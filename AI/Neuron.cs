namespace AI;

public class Neuron
{
    public NeuronType Type { get; set; }
    public double Charge { get; set; }
    public Neuron? Output0 { get; set; }
    public int? Output0Delay { get; set; }
    public double? Output0Weight { get; set; }
    public Neuron? Output1 { get; set; }
    public int? Output1Delay { get; set; }
    public double? Output1Weight { get; set; }
    public Neuron? Output2 { get; set; }
    public int? Output2Delay { get; set; }
    public double? Output2Weight { get; set; }
    public List<Neuron> Inputs { get; set; }
    public List<Spike> SpikeHistory { get; set; }
    public IOBuffer? IOBuffer;
    public IOBufferRole? IOBufferRole;
    public bool WasSpiked { get; set; }
}
