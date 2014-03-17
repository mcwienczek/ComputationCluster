﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;

namespace Communication_Library
{
    [Serializable]
    public class SolveRequestMessage : ComputationMessage
    {
        [XmlElement]
        public string ProblemType { get; set; }
        [XmlElement]
        public long SolvingTimeout { get; set; }
        [XmlElement]
        public string Data { get; set; }
    }
}
