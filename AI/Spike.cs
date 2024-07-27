namespace AI;

public class Spike
{
    public long ArrivalTime;
    public byte Charge;
    public Neuron Target;
    public Neuron? Source; //Some spikes originate from beyond the veil as background noise + energy injection.
}
