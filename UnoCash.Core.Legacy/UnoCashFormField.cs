using System;
using System.Collections.Generic;
using Azure.AI.FormRecognizer.Models;
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedMember.Global

namespace UnoCash.Core
{
    public class UnoCashFormField
    {
        public string Name { get; set; }
        public UnoCashFieldData LabelData { get; set; }
        public UnoCashFieldData ValueData { get; set; }
        public UnoCashFieldValue Value { get; set; }
        public float Confidence { get; set; }
    }

    public class UnoCashFieldData
    {
        public string Text { get; set; }
        public IReadOnlyList<UnoCashFieldData> FieldElements { get; set; }
    }
    
    public class UnoCashFieldValue
    {
        public FieldValueType ValueType { get; set; }
        public string ValueString { get; set; }
        public long ValueInteger { get; set; }
        public float ValueNumber { get; set; }
        public DateTime ValueDate { get; set; }
        public TimeSpan ValueTime { get; set; }
        public IReadOnlyList<UnoCashFormField> ValueList { get; set; }
        public IReadOnlyDictionary<string, UnoCashFormField> ValueDictionary { get; set; }
    }
}