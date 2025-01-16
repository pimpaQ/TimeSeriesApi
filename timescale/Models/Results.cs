namespace timescale.Models
{
    public class Results
    {
        public int ID { get; set; }
        public string FileName { get; set; }
        public TimeSpan DeltaTime { get; set; }
        public DateTime MinDate { get; set; }
        public double AvgExecutionTime { get; set; }
        public double AvgValue { get; set; }
        public double MedianValue { get; set; }
        public double MaxValue { get; set; }
        public double MinValue { get; set; }
    }
}
