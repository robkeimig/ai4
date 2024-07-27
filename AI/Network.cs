namespace AI;

internal class Network
{
    readonly int _maxConnectivityDelayTicks;
    readonly int _minConnectivityDelayTicks;
    readonly int _spikeInjectionIntervalTicks;
    readonly int _ticksPerChargeDecimation;
    readonly Neuron[] _neurons;
    readonly IOBuffer[] _ioBuffers;
    readonly LcgRandom _random;

    public Network(
        long neuronCount,
        int maxConnectivityDelayTicks = 5_000,
        int minConnectivityDelayTicks = 100,
        int spikeInjectionIntervalTicks = 100,
        int ticksPerChargeDecimation = 1_000,
        long randomSeed = 123)
    {
        _maxConnectivityDelayTicks = maxConnectivityDelayTicks;
        _minConnectivityDelayTicks = minConnectivityDelayTicks;
        _spikeInjectionIntervalTicks = spikeInjectionIntervalTicks;
        _ticksPerChargeDecimation = ticksPerChargeDecimation;
        _random = new LcgRandom(randomSeed);

        var neurons = new Neuron[neuronCount];

        for (int x = 0; x < neurons.Length; x++)
        {
            var neuron = new Neuron
            {
                SpikeHistory = new List<Spike>(),
                Inputs = new List<Neuron>(),
            };

            neurons[x] = neuron;
        }

        for (int x = 0; x < neurons.Length; x++)
        {
            if (x % 10_000 == 0)
            {
                Console.WriteLine("Creating Neurons: " + 100.0 * x / neurons.Length + "% complete.");
            }

            //Calculate random, unique indexes for other neurons that we will make our downstream peers.
            var out0Index = _random.NextInt32(0, neurons.Length, new List<int> { x });
            var out1Index = _random.NextInt32(0, neurons.Length, new List<int> { x, out0Index });
            var out2Index = _random.NextInt32(0, neurons.Length, new List<int> { x, out0Index, out1Index });

            var neuron = neurons[x];

            //Map the object references.
            neuron.Output0 = neurons[out0Index];
            neuron.Output1 = neurons[out1Index];
            neuron.Output2 = neurons[out2Index];

            //Calculate random delays between this neuron and the downstream peers.
            neuron.Output0Delay = _random.NextInt32(_minConnectivityDelayTicks, _maxConnectivityDelayTicks);
            neuron.Output1Delay = _random.NextInt32(_minConnectivityDelayTicks, _maxConnectivityDelayTicks);
            neuron.Output2Delay = _random.NextInt32(_minConnectivityDelayTicks, _maxConnectivityDelayTicks);

            //Initialize weights randomly. These are what we will be training.
            neuron.Output0Weight = _random.NextByte();
            neuron.Output1Weight = _random.NextByte();
            neuron.Output2Weight = _random.NextByte();

            //Add backreferences to the current neuron to the downstream peers we established above.
            neuron.Output0.Inputs.Add(neuron);
            neuron.Output1.Inputs.Add(neuron);
            neuron.Output2.Inputs.Add(neuron);
        }

        var removedNeurons = 0;

        do
        {
            var neuronsToRemove = new Dictionary<Neuron, long>();

            for (int x = 0; x < neurons.Length; x++)
            {
                var neuron = neurons[x];

                if (neuron != null)
                {
                    if (!neuron.Inputs.Any())
                    {
                        neuron.Output0.Inputs.Remove(neuron);
                        neuron.Output1.Inputs.Remove(neuron);
                        neuron.Output2.Inputs.Remove(neuron);
                        neuronsToRemove.Add(neuron, x);
                        neurons[x] = null;
                    }
                }
            }

            Console.WriteLine($"Pruning {neuronsToRemove.Count} unused neurons.");
            removedNeurons = neuronsToRemove.Count;
        }
        while (removedNeurons > 0);

        //Compact the array.
        var finalNeuronCount = neurons.Count(x => x?.Inputs.Any() ?? false);
        var finalNeuronArray = new Neuron[finalNeuronCount];
        var index = 0;

        foreach (var neuron in neurons.Where(x => x is not null))
        {
            finalNeuronArray[index++] = neuron;
        }

        _neurons = finalNeuronArray;
        Console.WriteLine($"Resulting Neuron count after pruning: {_neurons.LongLength}");
    }

    /// <summary>
    /// clone
    /// </summary>
    /// <param name="parent"></param>
    public Network(Network parent) 
    { 
    }
}
