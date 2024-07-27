namespace AI;

internal class Network
{
    readonly int _maxConnectivityDelayTicks;
    readonly int _minConnectivityDelayTicks;
    readonly int _spikeInjectionIntervalTicks;
    readonly int _ticksPerChargeDecimation;
    readonly Neuron _energyInjectionNeuron;
    readonly Neuron[] _neurons;
    readonly List<IOBuffer> _ioBuffers;
    readonly LcgRandom _random;
    readonly List<int> _ioBufferExclusions; 

    public Network(
        long neuronCount,
        List<IOBuffer> ioBuffers,
        int maxConnectivityDelayTicks = 5_000,
        int minConnectivityDelayTicks = 100,
        int spikeInjectionIntervalTicks = 100,
        int ticksPerChargeDecimation = 1_000,
        long randomSeed = 333)
    {
        _maxConnectivityDelayTicks = maxConnectivityDelayTicks;
        _minConnectivityDelayTicks = minConnectivityDelayTicks;
        _spikeInjectionIntervalTicks = spikeInjectionIntervalTicks;
        _ticksPerChargeDecimation = ticksPerChargeDecimation;
        _random = new LcgRandom(randomSeed);
        _ioBuffers = new List<IOBuffer>();
        _ioBufferExclusions = new List<int>();

        var neurons = new Neuron[neuronCount];

        for (int x = 0; x < neurons.Length; x++)
        {
            var neuron = new Neuron
            {
                SpikeHistory = new List<Spike>(),
                Inputs = new List<Neuron>(),
                Type = _random.NextInt32(0,10) switch
                {
                    0 => NeuronType.Inhibitory,
                    1 => NeuronType.Inhibitory,

                    2 => NeuronType.Excitatory,
                    3 => NeuronType.Excitatory,

                    4 => NeuronType.Excitatory,
                    5 => NeuronType.Excitatory,

                    6 => NeuronType.Excitatory,
                    7 => NeuronType.Excitatory,

                    8 => NeuronType.Excitatory,
                    9 => NeuronType.Excitatory,
                }
            };

            neurons[x] = neuron;
        }

        for (int x = 0; x < neurons.Length; x++)
        {
            if (x % 10_000 == 0)
            {
                Console.WriteLine("Mapping Neurons: " + 100.0 * x / neurons.Length + "% complete.");
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

        //Map IO Buffers.
        foreach (var iobuffer in ioBuffers)
        {
            _ioBuffers.Add(iobuffer);

            //Map common cursor handling neurons.
            var cursorIncrementerNeuron = NextWellConnectedNonIoNeuron();
            cursorIncrementerNeuron.IOBuffer = iobuffer;
            cursorIncrementerNeuron.IOBufferRole = IOBufferRole.CursorIncrementer;
            iobuffer.AssignedNeurons.Add(cursorIncrementerNeuron);
            _ioBufferExclusions.Add(Array.IndexOf(_neurons, cursorIncrementerNeuron));

            var cursorDecrementerNeuron = NextWellConnectedNonIoNeuron();
            cursorDecrementerNeuron.IOBuffer = iobuffer;
            cursorDecrementerNeuron.IOBufferRole = IOBufferRole.CursorDecrementer;
            iobuffer.AssignedNeurons.Add(cursorDecrementerNeuron);
            _ioBufferExclusions.Add(Array.IndexOf(_neurons, cursorDecrementerNeuron));

            var cursorMinLimitNotifierNeuron = NextWellConnectedNonIoNeuron();
            cursorMinLimitNotifierNeuron.IOBuffer = iobuffer;
            cursorMinLimitNotifierNeuron.IOBufferRole = IOBufferRole.CursorMinLimitNotifier;
            iobuffer.AssignedNeurons.Add(cursorMinLimitNotifierNeuron);
            _ioBufferExclusions.Add(Array.IndexOf(_neurons, cursorMinLimitNotifierNeuron));

            var cursorMaxLimitNotifierNeuron = NextWellConnectedNonIoNeuron();
            cursorMaxLimitNotifierNeuron.IOBuffer = iobuffer;
            cursorMaxLimitNotifierNeuron.IOBufferRole = IOBufferRole.CursorMaxLimitNotifier;
            iobuffer.AssignedNeurons.Add(cursorMaxLimitNotifierNeuron);
            _ioBufferExclusions.Add(Array.IndexOf(_neurons, cursorMaxLimitNotifierNeuron));

            //If we have effective read access to the buffer, map the neuron responsible for reading the value as a time delta encoded spike pair.
            if (iobuffer.Access == IOBufferAccess.ReadWrite || iobuffer.Access == IOBufferAccess.Read)
            {
                var cursorReadNeuron = NextWellConnectedNonIoNeuron();  //The neuron that triggers the cursor read. This is from network activation.
                cursorReadNeuron.IOBuffer = iobuffer;
                cursorReadNeuron.IOBufferRole = IOBufferRole.CursorRead;
                iobuffer.AssignedNeurons.Add(cursorReadNeuron);
                _ioBufferExclusions.Add(Array.IndexOf(_neurons, cursorReadNeuron));

                var cursorReadOutputNeuron = NextWellConnectedNonIoNeuron();    //The neuron that is activated by the buffer reading operation. This is from buffer to network spikes.
                cursorReadOutputNeuron.IOBuffer = iobuffer;
                cursorReadOutputNeuron.IOBufferRole = IOBufferRole.CursorReadOutput;
                iobuffer.AssignedNeurons.Add(cursorReadOutputNeuron);
                _ioBufferExclusions.Add(Array.IndexOf(_neurons, cursorReadOutputNeuron));
            }

            if (iobuffer.Access == IOBufferAccess.ReadWrite || iobuffer.Access == IOBufferAccess.Write)
            {
                var cursorWriteNeuron = NextWellConnectedNonIoNeuron();  //The neuron that triggers the cursor write. This is from network activation.
                cursorWriteNeuron.IOBuffer = iobuffer;
                cursorWriteNeuron.IOBufferRole = IOBufferRole.CursorWrite;
                iobuffer.AssignedNeurons.Add(cursorWriteNeuron);
                _ioBufferExclusions.Add(Array.IndexOf(_neurons, cursorWriteNeuron));

                var cursorWriteInputNeuron = NextWellConnectedNonIoNeuron();    //The neuron that is *sampled* by buffer writing operation. This is a sidechannel technique using spike history on the neuron.
                cursorWriteInputNeuron.IOBuffer = iobuffer;
                cursorWriteInputNeuron.IOBufferRole = IOBufferRole.CursorWriteInput;
                iobuffer.AssignedNeurons.Add(cursorWriteInputNeuron);
                _ioBufferExclusions.Add(Array.IndexOf(_neurons, cursorWriteInputNeuron));
            }
        }

        _energyInjectionNeuron = NextWellConnectedNonIoNeuron();
    }

    /// <summary>
    /// clone
    /// </summary>
    /// <param name="parent"></param>
    public Network(Network parent) 
    { 
    }

    public string Statistics()
    {
        var inhibitoryNeurons = _neurons.Count(x => x.Type == NeuronType.Inhibitory);
        var excitatoryNeurons = _neurons.Count(x => x.Type == NeuronType.Excitatory);

        var inhibPercent = inhibitoryNeurons / (double)_neurons.Length;
        var exitPercent = excitatoryNeurons / (double)_neurons.Length;

        return $@"{inhibPercent} : {exitPercent}";
    }

    public void Simulate(long timeLimitTicks)
    {
        var spikeQueue = new PriorityQueue<Spike, long>();

        for (long t = 0; t < timeLimitTicks; t++)
        {
            if (t % 1_000_000 == 0)
            {
                Console.WriteLine($"Network Simulating t = {t}");
            }

            //Process items from the priority queue until either:
            //1. It is empty.
            //2. The minimal priority is after the current tick.
            while (spikeQueue.TryPeek(out Spike spike, out long priority))
            {
                //Console.WriteLine(t + " " + spikeQueue.Count + " " + spike.Target);
                if (priority <= t)
                {
                    spikeQueue.Dequeue();
                    ProcessSpike(t, spikeQueue, spike);
                }
                else
                {
                    break;
                }
            }

            //Inject random energy into the network
            if (t % _spikeInjectionIntervalTicks == 0)
            {
                spikeQueue.Enqueue(new Spike
                {
                    Charge = 0xFF,
                    ArrivalTime = t,
                    Target = _energyInjectionNeuron
                }, t);
            }
        }
    }

    private void ProcessSpike(long t, PriorityQueue<Spike, long> spikeQueue, Spike spike)
    {
        //...
    }

    private Neuron NextWellConnectedNonIoNeuron() => 
        _neurons.First(x => 
            x.Inputs.Count > 3 
        &&  x.Output0 != null 
        &&  x.Output1 != null 
        &&  x.Output2 != null 
        &&  x.IOBuffer == null);
}
