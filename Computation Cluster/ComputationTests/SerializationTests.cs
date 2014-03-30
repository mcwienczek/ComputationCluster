﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Communication_Library;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace ComputationTests
{
    [TestClass]
    public class SerializationTests
    {
        [DeploymentItem(@"XMLTestData\SolveRequestMessage.xml", "XMLTestData")]
        [TestMethod]
        public void SolveRequestMessageSerialization()
        {
            string testData = System.IO.File.ReadAllText(@"XMLTestData\SolveRequestMessage.xml");
            var serializer = new ComputationSerializer<SolveRequestMessage>();
            var solveRequestMessage = new SolveRequestMessage()
            {
                Data = new byte[]{0,0,25},
                ProblemType = "TSP",
                SolvingTimeout = 15
            };
            var result = serializer.Serialize(solveRequestMessage);
            Assert.AreEqual(result, testData);
        }

        [DeploymentItem(@"XMLTestData\SolveRequestMessage.xml", "XMLTestData")]
        [TestMethod]
        public void RegisterMessageSerialization()
        {
            string testData = System.IO.File.ReadAllText(@"XMLTestData\SolveRequestMessage.xml");
            var serializer = new ComputationSerializer<RegisterMessage>();
            var registerMessage = new RegisterMessage()
            {
                Type = RegisterType.ComputationalNode,
                SolvableProblems = new string[]{"ab","ba"},
                ParallelThreads = 15
            };
            var result = serializer.Serialize(registerMessage);
            Assert.AreEqual(result, testData);
        }
    }
}