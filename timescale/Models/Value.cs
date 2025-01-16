namespace timescale.Models
{
    public class Value
    {
        public int ID { get; set; }
        public DateTime Date { get; set; }
        public double ExecutionTime { get; set; }
        public double IndicatorValue { get; set; }
        public string FileName { get; set; }
    }
}
