﻿////------------------------------------------------------------------------------
//// <auto-generated>
////     This code was generated by a tool.
////     Runtime Version:4.0.30319.18444
////
////     Changes to this file may cause incorrect behavior and will be lost if
////     the code is regenerated.
//// </auto-generated>
////------------------------------------------------------------------------------

//using System.Xml.Serialization;

//// 
//// This source code was auto-generated by xsd, Version=4.0.30319.17929.
//// 

//namespace Communication_Library
//{

//    /// <remarks/>
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.17929")]
//    [System.SerializableAttribute()]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://www.mini.pw.edu.pl/ucc/")]
//    [System.Xml.Serialization.XmlRootAttribute(Namespace = "http://www.mini.pw.edu.pl/ucc/", IsNullable = false)]
//    public partial class SolvePartialProblems
//    {

//        private string problemTypeField;

//        private ulong idField;

//        private byte[] commonDataField;

//        private ulong solvingTimeoutField;

//        private bool solvingTimeoutFieldSpecified;

//        private SolvePartialProblemsPartialProblem[] partialProblemsField;

//        /// <remarks/>
//        public string ProblemType
//        {
//            get
//            {
//                return this.problemTypeField;
//            }
//            set
//            {
//                this.problemTypeField = value;
//            }
//        }

//        /// <remarks/>
//        public ulong Id
//        {
//            get
//            {
//                return this.idField;
//            }
//            set
//            {
//                this.idField = value;
//            }
//        }

//        /// <remarks/>
//        [System.Xml.Serialization.XmlElementAttribute(DataType = "base64Binary")]
//        public byte[] CommonData
//        {
//            get
//            {
//                return this.commonDataField;
//            }
//            set
//            {
//                this.commonDataField = value;
//            }
//        }

//        /// <remarks/>
//        public ulong SolvingTimeout
//        {
//            get
//            {
//                return this.solvingTimeoutField;
//            }
//            set
//            {
//                this.solvingTimeoutField = value;
//            }
//        }

//        /// <remarks/>
//        [System.Xml.Serialization.XmlIgnoreAttribute()]
//        public bool SolvingTimeoutSpecified
//        {
//            get
//            {
//                return this.solvingTimeoutFieldSpecified;
//            }
//            set
//            {
//                this.solvingTimeoutFieldSpecified = value;
//            }
//        }

//        /// <remarks/>
//        [System.Xml.Serialization.XmlArrayItemAttribute("PartialProblem", IsNullable = false)]
//        public SolvePartialProblemsPartialProblem[] PartialProblems
//        {
//            get
//            {
//                return this.partialProblemsField;
//            }
//            set
//            {
//                this.partialProblemsField = value;
//            }
//        }
//    }

//    /// <remarks/>
//    [System.CodeDom.Compiler.GeneratedCodeAttribute("xsd", "4.0.30319.17929")]
//    [System.SerializableAttribute()]
//    [System.Diagnostics.DebuggerStepThroughAttribute()]
//    [System.ComponentModel.DesignerCategoryAttribute("code")]
//    [System.Xml.Serialization.XmlTypeAttribute(AnonymousType = true, Namespace = "http://www.mini.pw.edu.pl/ucc/")]
//    public partial class SolvePartialProblemsPartialProblem
//    {

//        private ulong taskIdField;

//        private byte[] dataField;

//        /// <remarks/>
//        public ulong TaskId
//        {
//            get
//            {
//                return this.taskIdField;
//            }
//            set
//            {
//                this.taskIdField = value;
//            }
//        }

//        /// <remarks/>
//        [System.Xml.Serialization.XmlElementAttribute(DataType = "base64Binary")]
//        public byte[] Data
//        {
//            get
//            {
//                return this.dataField;
//            }
//            set
//            {
//                this.dataField = value;
//            }
//        }
//    }
//}