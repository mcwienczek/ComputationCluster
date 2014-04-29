﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design.Serialization;
using System.Configuration;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using Communication_Library;
using log4net;

namespace Computational_Server
{
    [MethodBoundary]
    public class ComputationServer : BaseNode
    {
        private Thread listeningThread;
        private Thread processingThread;
        private CancellationTokenSource listeningThreadCancellationTokenSource;
        private CancellationTokenSource processingThreadCancellationToken;

        private ConcurrentDictionary<ulong, SolveRequestMessage> solveRequests;
        private Dictionary<ulong, NodeEntry> activeNodes;
        private List<SolutionsMessage> partialSolutions;
        private Dictionary<ulong, PartialProblemsMessage> partialProblems;
        private ICommunicationModule communicationModule;
        public readonly int processingThreadSleepTime;
        public readonly TimeSpan DefaultTimeout;
        private ulong nodesId;
        private object nodesIdLock = new object();
        private Socket serverSocket;
        private ulong solutionId;
        private object solutionIdLock = new object();
        MethodInfo serializeMessageMethod;
        private List<SolutionsMessage> finalSolutions;

        public ComputationServer(TimeSpan nodeTimeout, ICommunicationModule communicationModule, int threadSleepTime)
        {
            solveRequests = new ConcurrentDictionary<ulong, SolveRequestMessage>();
            activeNodes = new Dictionary<ulong, NodeEntry>();
            finalSolutions = new List<SolutionsMessage>();
            partialSolutions = new List<SolutionsMessage>();
            DefaultTimeout = nodeTimeout;
            nodesId = 1;
            solutionId = 1;
            this.communicationModule = communicationModule;
            this.processingThreadSleepTime = threadSleepTime;
            partialProblems = new Dictionary<ulong, PartialProblemsMessage>();
            serializeMessageMethod = typeof(ComputationServer).GetMethod("SerializeMessage");
        }

        public void StartServer()
        {
            StartListeningThread();
            StartProcessingThread();
        }

        private void StartListeningThread()
        {
            listeningThreadCancellationTokenSource = new CancellationTokenSource();
            listeningThread = new Thread(ListeningThread);
            listeningThread.Start();
        }

        private void StartProcessingThread()
        {
            processingThreadCancellationToken = new CancellationTokenSource();
            processingThread = new Thread(ProcessingThread);
            processingThread.Start();
        }

        private void ProcessingThread()
        {
            while (!processingThreadCancellationToken.IsCancellationRequested)
            {
                RemoveUnusedNodes();

                Thread.Sleep(processingThreadSleepTime);
            }
        }

        private void RemoveUnusedNodes()
        {
            lock (activeNodes)
            {
                var nodesToDelete = activeNodes.Where(x => HasNodeExpired(x.Value)).ToList();
                for (int i = 0; i < nodesToDelete.Count; i++)
                {
                    var nodeToDelete = nodesToDelete[i];
                    NodeEntry deletedNode = null;
                    lock (activeNodes)
                    {
                        if (!activeNodes.Remove(nodeToDelete.Key))
                            _logger.Error("Could not remove node from activeNodes list. NodeId: " + nodeToDelete.Key);
                    }
                    _logger.Debug("Removed node from activeNodes list. NodeId: " + nodeToDelete.Key);
                }
            }
        }

        private bool HasNodeExpired(NodeEntry x)
        {
            return (DateTime.Now - x.LastStatusSentTime) > DefaultTimeout;
        }

        public void ListeningThread()
        {
            serverSocket = communicationModule.SetupServer();
            while (!listeningThreadCancellationTokenSource.IsCancellationRequested)
            {
                var clientSocket = communicationModule.Accept(serverSocket);

                var receivedMessage = communicationModule.ReceiveData(clientSocket);

                string result = String.Empty;
                if (!String.IsNullOrEmpty(receivedMessage))
                    result = ProcessMessage(receivedMessage);

                if(!String.IsNullOrEmpty(result))
                    communicationModule.SendData(result, clientSocket);

                communicationModule.CloseSocket(clientSocket);
            }
        }

        private string ProcessMessage(string message)
        {
            var messageName = this.GetMessageName(message);
            switch (messageName)
            {
                case "Register":
                    return this.ProcessCaseRegister(message);

                case "SolveRequest":
                    return this.ProcessCaseSolveRequest(message);

                case "SolutionRequest":
                    return this.ProcessCaseSolutionRequest(message);

                case "Status":
                    return this.ProcessCaseStatus(message);

                case "SolvePartialProblems":
                    return this.ProcessCaseSolvePartialProblems(message);

                case "Solutions":
                    return this.ProcessCaseSolutions(message);
                    
                default:
                    break;
            }
            return String.Empty;
        }

        private string ProcessCaseRegister(string message)
        {
            var registerMessage = DeserializeMessage<RegisterMessage>(message);

            var newId = GenerateNewNodeId();

            RegisterNode(newId, registerMessage.Type, registerMessage.SolvableProblems.ToList(),
                registerMessage.ParallelThreads);

            var registerResponse = new RegisterResponseMessage()
            {
                Id = newId,
                TimeoutTimeSpan = this.DefaultTimeout
            };

            return SerializeMessage(registerResponse);
        }

        private ulong GenerateNewNodeId()
        {
            ulong newNodeId = 0;
            lock (nodesIdLock)
            {
                newNodeId = nodesId;
                this.nodesId++;
            }
            return newNodeId;
        }

        /// <summary>
        /// Registers node in server and adds it to active nodes queue
        /// </summary>
        /// <param name="newId"></param>
        /// <param name="type"></param>
        /// <param name="solvableProblems"></param>
        /// <param name="parallelThreads"></param>
        /// <returns>0 if success, negative value if there was an error</returns>
        private void RegisterNode(ulong newId, RegisterType type, List<string> solvableProblems, byte parallelThreads)
        {
            var node = new NodeEntry(newId, type, solvableProblems, parallelThreads);
            lock (activeNodes)
            {
                activeNodes.Add(newId, node);
                _logger.Debug("Node added to server list");
            }
        }

        private string ProcessCaseSolutions(string message)
        {
            var deserializedMessage = DeserializeMessage<SolutionsMessage>(message);

            //TODO merge rozwiazan i spr czy wszystkie partialSolutions sa juz rozwiazane
            SolutionsMessage oldSolutions = null;
            if (IsFinal(deserializedMessage))
            {
                lock (finalSolutions)
                {
                    finalSolutions.Add(deserializedMessage);
                }
            }

            lock (partialSolutions)
            {
                oldSolutions = partialSolutions.FirstOrDefault(x => x.Id == deserializedMessage.Id);
                MergeSolutions(oldSolutions, deserializedMessage);
            }

            if(oldSolutions == null)
                throw new Exception("Could not find solution for solutionId: " + deserializedMessage.Id);

            return string.Empty;
        }

        private bool IsFinal(SolutionsMessage solutionsMessage)
        {
            bool final = true;
            for (int i = 0; i < solutionsMessage.Solutions.Length; i++)
            {
                var solution = solutionsMessage.Solutions[i];
                if (solution.Type != SolutionType.Final)
                    final = false;
            }
            return final;
        }

        private bool AllPartialSolutionSolved(SolutionsMessage oldSolutions)
        {
            bool solved = true;
            for (int i = 0; i < oldSolutions.Solutions.Length; i++)
            {
                var solution = oldSolutions.Solutions[i];
                if (solution.Type != SolutionType.Partial)
                    solved = false;
            }
            return solved;
        }

        private string ProcessCaseSolvePartialProblems(string message)
        {
            var deserializedMessage = DeserializeMessage<PartialProblemsMessage>(message);
            lock (partialProblems)
            {
                partialProblems.Add(deserializedMessage.Id, deserializedMessage);
            }
            return string.Empty;
        }

        private void MergeSolutions(SolutionsMessage oldSolutionsMessage, SolutionsMessage newSolutionsMessage)
        {
            for (int i = 0; i < newSolutionsMessage.Solutions.Length; i++)
            {
                var newSolution = newSolutionsMessage.Solutions[i];
                var oldSolution = oldSolutionsMessage.Solutions.FirstOrDefault(x => x.TaskId == newSolution.TaskId);
                if (oldSolution == null)
                    throw new Exception("Could not find task for taskId: " + newSolution.TaskId + ", problemId: " + newSolutionsMessage.Id);
                oldSolution.Data = newSolution.Data;
                oldSolution.ComputationsTime = newSolution.ComputationsTime;
                oldSolution.TimeoutOccured = newSolution.TimeoutOccured;
                oldSolution.Type = newSolution.Type;
            }
        }

        private string ProcessCaseStatus(string message)
        {
            var deserializedStatusMessage = DeserializeMessage<StatusMessage>(message);
            _logger.Info("Received status from nodeId: " + deserializedStatusMessage.Id);
            
            UpdateNodesLifetime(deserializedStatusMessage);

            var node = GetActiveNode(deserializedStatusMessage.Id);
            if (node == null)
                return String.Empty;
            var nodeTask = GetTaskForNode(node);
            if (nodeTask == null)
                return String.Empty;

            //TODO update solutions from status when received from CN

            var declaringType = nodeTask.GetType();
            
            MethodInfo generic = serializeMessageMethod.MakeGenericMethod(declaringType);

            return (string)generic.Invoke(this, new object[] { nodeTask });
        }

        private ComputationMessage GetTaskForNode(NodeEntry node)
        {
            switch (node.Type)
            {
                case RegisterType.TaskManager:
                    return GetTaskForTaskManager(node);
                case RegisterType.ComputationalNode:
                    return GetTaskForComputationalNode(node);
                default:
                    _logger.Error("GetTaskForNode error: Unknown node type");
                    return null;
            }
        }

        private ComputationMessage GetTaskForComputationalNode(NodeEntry node)
        {
            PartialProblemsMessage partialProblem = null;
            lock (partialProblems)
            {
                if (partialProblems.Any(x => node.SolvingProblems.Contains(x.Value.ProblemType)))
                {
                    var partialProblemKeyValue =
                       partialProblems.FirstOrDefault(x => node.SolvingProblems.Contains(x.Value.ProblemType));
                    partialProblem = partialProblemKeyValue.Value;
                }
            }
            return partialProblem;
        }

        private ComputationMessage GetTaskForTaskManager(NodeEntry node)
        {
            var divideMessage = GetDivideProblemMessageForType(node.SolvingProblems);
            if (divideMessage != null)
                return divideMessage;
            var partialSolutionsMessage = GetPartialSolutionForType(node.Type);
            if (partialSolutionsMessage != null)
                return partialSolutionsMessage;
            return null;
        }

        private SolutionsMessage GetPartialSolutionForType(RegisterType type)
        {
            var partialSolution = partialSolutions.FirstOrDefault(AllPartialSolutionSolved);
            return partialSolution;
        }

        /// <summary>
        /// Dequeues first solveReqest of given type and returns appropiate DivideProblemMessage for SolveRequest
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private DivideProblemMessage GetDivideProblemMessageForType(List<string> solvingTypes)
        {
            DivideProblemMessage divideProblemMessage = null;
            KeyValuePair<ulong, SolveRequestMessage> solveRequest = new KeyValuePair<ulong,SolveRequestMessage>();
            lock(solveRequests)
            {
                solveRequest = solveRequests.FirstOrDefault(x => solvingTypes.Contains(x.Value.ProblemType));
            }

            if (solveRequest.Value != null)
            {
                divideProblemMessage = new DivideProblemMessage()
                {
                    ComputationalNodes = (ulong)activeNodes.Count,
                    Data = solveRequest.Value.Data,
                    ProblemType = solveRequest.Value.ProblemType,
                    Id = solveRequest.Key
                };
            }
            
            return divideProblemMessage;
        }

        private NodeEntry GetActiveNode(ulong nodeId)
        {
            NodeEntry node = null;
            if (!activeNodes.TryGetValue(nodeId, out node))
            {
                string errorMessage = "Could not get value of nodeId: " + nodeId + " from dictionary.";
                _logger.Error(errorMessage);
            }
            return node;
        }

        private void UpdateNodesLifetime(StatusMessage statusMessage)
        {
            try
            {
                lock (activeNodes)
                {
                    NodeEntry node = null;
                    lock (activeNodes)
                    {
                        node = activeNodes[statusMessage.Id];
                        if (node == null)
                        {
                            _logger.Error("Error updating node lifetime. Could not find node: " + statusMessage.Id);
                            return;
                        }
                        node.LastStatusSentTime = DateTime.Now;
                        _logger.Debug("Updated node lifetime. Nodeid: " + statusMessage.Id);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.Error("Could not update nodes lifetime. NodeId: " + statusMessage.Id + ". Exception: " + ex.ToString());
            }
        }

        private string ProcessCaseSolutionRequest(string message)
        {
            throw new NotImplementedException();
            var deserializedSolutionRequestMessage = DeserializeMessage<SolutionRequestMessage>(message);


            return SerializeMessage<SolutionsMessage>(null);
        }

        private string ProcessCaseSolveRequest(string message)
        {
            var deserializedMessage = DeserializeMessage<SolveRequestMessage>(message);
            
            ulong solutionId = GenerateNewSolutionId();

            var solveRequestResponse = new SolveRequestResponseMessage() { Id = solutionId };
            if (!solveRequests.TryAdd(solutionId, deserializedMessage))
            {
                _logger.Error("Could not add SolveRequest to dictionary. SolutionId: " + solutionId + ", message: " + deserializedMessage);
                solveRequestResponse.Id = 0;
            }
            
            return SerializeMessage(solveRequestResponse);
        }

        private ulong GenerateNewSolutionId()
        {
            ulong newSolutionId = 0;
            lock (solutionIdLock)
            {
                newSolutionId = solutionId;
                solutionId++;
            }
            return newSolutionId;
        }

        public void StopServer()
        {
            listeningThreadCancellationTokenSource.Cancel();
            listeningThread.Join();
            _logger.Info("Stopped listening");
        }
    }
}