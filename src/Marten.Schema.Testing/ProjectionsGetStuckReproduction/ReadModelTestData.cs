using System;
using System.Collections.Generic;
using System.Text;

namespace Marten.Schema.Testing.ProjectionsGetStuckReproduction
{
    public class ReadModelTestData
    {
        public int MessageId { get; set; }
        public Guid Id { get; set; }
        public string P1 { get; set; }
        public string P2 { get; set; }
        public string P3 { get; set; }
        public string P4 { get; set; }
        public string P5 { get; set; }
        public string P6 { get; set; }
        public string P7 { get; set; }
        public string P8 { get; set; }
        public string P9 { get; set; }
        public string P10 { get; set; }
        public string P11 { get; set; }
        public string P12 { get; set; }
        public string P13 { get; set; }
        public string P14 { get; set; }
        public string P15 { get; set; }
        public string P16 { get; set; }

        public List<string> ListOfProperties { get; set; }

        public Guid CreatedByEventId { get; set; }
    }
}
