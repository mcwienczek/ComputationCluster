﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using Communication_Library;
using log4net;
using UCCTaskSolver;
using System.Threading;
using System.Xml;
using DynamicVehicleRoutingProblem;
using System.Collections.Concurrent;

namespace Task_Manager
{
    public class TaskManager : BaseNode
    {
        private static readonly ILog _logger =
            LogManager.GetLogger(System.Reflection.MethodBase.GetCurrentMethod().DeclaringType);

        private int serverPort;
        private string serverIp;
        private ICommunicationModule communicationModule;
        public TSP tsp;
        public ulong NodeId { get; set; }
        public TimeSpan Timeout { get; set; }
        private Socket socket;

        private Thread statusThread;
        private Thread processingThread;
        private CancellationTokenSource statusThreadCancellationTokenSource;
        private CancellationTokenSource processingThreadCancellationToken;
        private DateTime startTime;
        private TaskSolver<DVRP> taskSolver;
        private StatusThreadState state;
        private ConcurrentQueue<DivideProblemMessage> divideProblemMessageQueue;

        public TaskManager(string serverIp, int serverPort)
        {
            communicationModule = new CommunicationModule(serverIp, serverPort, 5000);
        }

        public void StartTM()
        {
            RegisterAtServer();
            StartStatusThread();
            StartProcessingThread();
            _logger.Info("Starting TM");
        }

        public void RegisterAtServer()
        {
            var registerMessage = new RegisterMessage()
            {
                ParallelThreads = 8,//???
                SolvableProblems = new string[] { "DVRP" },
                Type = RegisterType.TaskManager
            };
            var messageString = SerializeMessage(registerMessage);
            //var messageBytes = CommunicationModule.ConvertStringToData(messageString);

            socket = communicationModule.SetupClient();
            communicationModule.Connect(socket);
            communicationModule.SendData(messageString, socket);

            var response = communicationModule.ReceiveData(socket);
            _logger.Info("Response: " + response.ToString());
            var deserializedResponse = DeserializeMessage<RegisterResponseMessage>(response);
            this.NodeId = deserializedResponse.Id;
            this.Timeout = deserializedResponse.TimeoutTimeSpan;
            _logger.Info("Response has been deserialized");
            //communicationModule.Disconnect();
        }

        private void StartStatusThread()
        {
            statusThreadCancellationTokenSource = new CancellationTokenSource();
            statusThread = new Thread(SendStatusThread);
            statusThread.Start();
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
                if (divideProblemMessageQueue.Count == 0)
                    state = StatusThreadState.Idle;
                else
                {
                    state = StatusThreadState.Busy;
                    DivideProblemMessage dpm;
                    divideProblemMessageQueue.TryDequeue(out dpm);
                    taskSolver = new TaskSolver<DVRP>(dpm.Data);
                    taskSolver.DivideProblem((int)dpm.ComputationalNodes);
                }
                //lock (ActiveNodes)
                //{
                //    ActiveNodes.RemoveAll(HasNodeExpired);
                //}
                Thread.Sleep(1000);
            }
        }

        //private bool HasNodeExpired(NodeEntry x)
        //{
        //    return (DateTime.Now - x.LastStatusSentTime) > DefaultTimeout;
        //}

        public void SendStatusThread()
        {
            socket = communicationModule.SetupClient();
            while (!statusThreadCancellationTokenSource.IsCancellationRequested)
            {
                //send status
                StatusMessage statusMessage = new StatusMessage();
                statusMessage.Id = this.NodeId;
                var st= new StatusThread() { HowLong = (ulong)(DateTime.Now-startTime).TotalMilliseconds, TaskId =1 , State = state, ProblemType = taskSolver.Name, ProblemInstanceId = 1, TaskIdSpecified = true };
                statusMessage.Threads = new StatusThread[] { st };
                var statusMessageString = SerializeMessage(statusMessage);
                communicationModule.SendData(statusMessageString, socket);

                var receivedMessage = communicationModule.ReceiveData(socket);
                string result = String.Empty;
                if (!String.IsNullOrEmpty(receivedMessage))
                    result = ProcessMessage(receivedMessage);

                if (!String.IsNullOrEmpty(result))
                    communicationModule.SendData(result, socket);

                communicationModule.CloseSocket(socket);
                Thread.Sleep(this.Timeout);
            }
        }

        private string ProcessMessage(string message)
        {
            var messageName = this.GetMessageName(message);
            //_logger.Debug("Received " + messageName);
            //_logger.Debug("XML Data: " + message);
            switch (messageName)
            {
                case "RegisterResponse":
                    return this.ProcessCaseRegisterResponse(message);

                case "DivideProblem":
                    return this.ProcessCaseDivideProblem(message);

                case "PartialProblems":
                    return this.ProcessCasePartialProblems(message);
                default:
                    break;
            }
            return String.Empty;
        }

        private string ProcessCaseRegisterResponse(string message)
        {
            var deserializedMessage = DeserializeMessage<SolutionsMessage>(message);

            return string.Empty;
        }

        private string ProcessCaseDivideProblem(string message)
        {
            var deserializedDivideProblemMessage = DeserializeMessage<DivideProblemMessage>(message);
            divideProblemMessageQueue.Enqueue(deserializedDivideProblemMessage);

            return string.Empty;
        }

        private string ProcessCasePartialProblems(string message)
        {
            var deserializedMessage = DeserializeMessage<PartialProblemsMessage>(message);

            return string.Empty;
        }

        private string GetMessageName(string message)
        {
            var doc = new XmlDocument();
            try
            {
                doc.LoadXml(message);
            }
            catch (Exception ex)
            {
                //_logger.Error("Error parsing xml document: " + message + "exception: " + ex.ToString());
                return String.Empty;

                //TODO logowanie
            }
            XmlElement root = doc.DocumentElement;
            return root.Name;
        }

        public void StopTM()
        {
            statusThreadCancellationTokenSource.Cancel();
            statusThread.Join();
            //_logger.Info("Stopped listening");
        }

        public void Disconnect()
        {
            //communicationModule.Disconnect();
        }

        //public void DivideProblem(string statusMessageResponse)
        //{
        //    var serializer = new ComputationSerializer<DivideProblemMessage>();
        //    DivideProblemMessage dpm = serializer.Deserialize(statusMessageResponse);

        //    tsp = new TSP(dpm.Data);
        //    tsp.DivideProblem((int)dpm.ComputationalNodes);
        //    SolvePartialProblemsPartialProblem[] solvepp = new SolvePartialProblemsPartialProblem[tsp.PartialProblems.Length];


        //    for (int i = 0; i < tsp.PartialProblems.Length; i++)
        //    {
        //        solvepp[i] = new SolvePartialProblemsPartialProblem() { Data = tsp.PartialProblems[i], TaskId = (ulong)i };    
        //    }

        //        communicationModule.Connect(socket);
           
        //    //SolvePartialProblemsPartialProblem sp = new SolvePartialProblemsPartialProblem() { Data = new byte[] { 1, 2, 3 }, TaskId = 4 };
        //    var partialproblems = new PartialProblemsMessage() { Id = dpm.Id, CommonData = dpm.Data, PartialProblems = solvepp, ProblemType = tsp.Name, SolvingTimeout = 30, SolvingTimeoutSpecified = true };
        //    //var msg = SerializeMessage<PartialProblemsMessage>(partialproblems);
        //    //var msgBytes = CommunicationModule.ConvertStringToData(msg);
        //    //communicationModule.SendData(msgBytes);

        //    //communicationModule.Disconnect();
        //}
    }
}
