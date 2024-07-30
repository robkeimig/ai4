namespace AI;

internal class Network
{
    readonly int _maxConnectivityDelayTicks;
    readonly int _minConnectivityDelayTicks;
    readonly int _spikeInjectionIntervalTicks;
    readonly Neuron _energyInjectionNeuron;
    readonly Neuron[] _neurons;
    readonly List<IOBuffer> _ioBuffers;
    readonly LcgRandom _random;
    readonly List<int> _ioBufferExclusions;
    long _totalEnqueuedSpikes;

    public Network(
        long neuronCount,
        List<IOBuffer> ioBuffers,
        int maxConnectivityDelayTicks = 10_000,
        int minConnectivityDelayTicks = 10,
        int spikeInjectionIntervalTicks = 10,
        long randomSeed = 123)
    {
        _maxConnectivityDelayTicks = maxConnectivityDelayTicks;
        _minConnectivityDelayTicks = minConnectivityDelayTicks;
        _spikeInjectionIntervalTicks = spikeInjectionIntervalTicks;
        _random = new LcgRandom(randomSeed);
        _ioBuffers = new List<IOBuffer>();
        _ioBufferExclusions = new List<int>();

        var neurons = new Neuron[neuronCount];

        for (int x = 0; x < neurons.Length; x++)
        {
            var neuron = new Neuron
            {
                Inputs = new List<Neuron>(),
                Type = (_random.NextInt32(0, 100) < 20) ? NeuronType.Inhibitory : NeuronType.Excitatory,
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
            neuron.Output0Weight = _random.NextDouble();
            neuron.Output1Weight = _random.NextDouble();
            neuron.Output2Weight = _random.NextDouble();

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

                var cursorWriteInputANeuron = NextWellConnectedNonIoNeuron();    //The neuron that is *sampled* by buffer writing operation. This is a sidechannel technique using spike history on the neuron.
                cursorWriteInputANeuron.IOBuffer = iobuffer;
                cursorWriteInputANeuron.IOBufferRole = IOBufferRole.CursorWriteInputA;
                iobuffer.AssignedNeurons.Add(cursorWriteInputANeuron);
                _ioBufferExclusions.Add(Array.IndexOf(_neurons, cursorWriteInputANeuron));

                var cursorWriteInputBNeuron = NextWellConnectedNonIoNeuron();    //The neuron that is *sampled* by buffer writing operation. This is a sidechannel technique using spike history on the neuron.
                cursorWriteInputBNeuron.IOBuffer = iobuffer;
                cursorWriteInputBNeuron.IOBufferRole = IOBufferRole.CursorWriteInputB;
                iobuffer.AssignedNeurons.Add(cursorWriteInputBNeuron);
                _ioBufferExclusions.Add(Array.IndexOf(_neurons, cursorWriteInputBNeuron));
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
        _neurons = new Neuron[parent._neurons.Length];
        _ioBuffers = new List<IOBuffer>();
        _ioBufferExclusions = new List<int>();
        _maxConnectivityDelayTicks = parent._maxConnectivityDelayTicks;
        _minConnectivityDelayTicks = parent._minConnectivityDelayTicks;
        _spikeInjectionIntervalTicks = parent._spikeInjectionIntervalTicks;
        _random = parent._random;

        var neuronCloneMap = new Dictionary<Neuron, Neuron>();
        var neuronCloneQueue = new Queue<Neuron>();
        neuronCloneQueue.Enqueue(parent._neurons[0]);

        while (neuronCloneQueue.Count > 0)
        {
            var current = neuronCloneQueue.Dequeue();

            if (!neuronCloneMap.ContainsKey(current))
            {
                var clone = new Neuron
                {
                    Inputs = new List<Neuron>(),
                    Type = current.Type
                };

                neuronCloneMap[current] = clone;

                if (current.Output0 != null && !neuronCloneMap.ContainsKey(current.Output0)) neuronCloneQueue.Enqueue(current.Output0);
                if (current.Output1 != null && !neuronCloneMap.ContainsKey(current.Output1)) neuronCloneQueue.Enqueue(current.Output1);
                if (current.Output2 != null && !neuronCloneMap.ContainsKey(current.Output2)) neuronCloneQueue.Enqueue(current.Output2);
            }
        }

        foreach (var kvp in neuronCloneMap)
        {
            var original = kvp.Key;
            var clone = kvp.Value;

            if (original.Output0 != null)
            {
                clone.Output0 = neuronCloneMap[original.Output0];
                clone.Output0Delay = original.Output0Delay;
                clone.Output0Weight = original.Output0Weight;
            }

            if (original.Output1 != null)
            {
                clone.Output1 = neuronCloneMap[original.Output1];
                clone.Output1Delay = original.Output1Delay;
                clone.Output1Weight = original.Output1Weight;
            }

            if (original.Output2 != null)
            {
                clone.Output2 = neuronCloneMap[original.Output2];
                clone.Output2Delay = original.Output2Delay;
                clone.Output2Weight = original.Output2Weight;
            }

            foreach (var input in original.Inputs)
            {
                clone.Inputs.Add(neuronCloneMap[input]);
            }
        }

        for (int x = 0; x < _neurons.Length; x++)
        {
            _neurons[x] = neuronCloneMap[parent._neurons[x]];
        }

        foreach (var ioBuffer in parent._ioBuffers)
        {
            var clonedIoBuffer = new IOBuffer(ioBuffer, neuronCloneMap);

            foreach (var ioBufferNeuron in ioBuffer.AssignedNeurons)
            {
                var clonedIoBufferNeuron = neuronCloneMap[ioBufferNeuron];
                clonedIoBufferNeuron.IOBuffer = clonedIoBuffer;
                clonedIoBufferNeuron.IOBufferRole = ioBufferNeuron.IOBufferRole;
                _ioBufferExclusions.Add(Array.IndexOf(_neurons, clonedIoBufferNeuron));
            }

            _ioBuffers.Add(clonedIoBuffer);
            
        }

        _energyInjectionNeuron = neuronCloneMap[parent._energyInjectionNeuron];
    }

    public long TotalSpikes => _totalEnqueuedSpikes;

    public IEnumerable<IOBuffer> IOBuffers => _ioBuffers;

    public double SpikedPercentage => (double)_neurons.Count(x => x.WasSpiked) / _neurons.Count();

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
            //if (t % 100_000 == 0)
            //{
            //    Console.WriteLine($"Network Simulating t = {t}; Queue = {spikeQueue.Count}");
            //}

            //Process items from the priority queue until either:
            //1. It is empty.
            //2. The minimal priority is after the current tick.
            while (spikeQueue.TryPeek(out Spike spike, out long priority))
            {
                //Console.WriteLine(t + " " + spikeQueue.Count + " " + spike.Target);
                if (priority <= t)
                {
                    spikeQueue.Dequeue();
                    ProcessPendingSpike(t, spikeQueue, spike);
                }
                else
                {
                    break;
                }
            }

            //Inject random energy into the network
            if (t % _spikeInjectionIntervalTicks == 0)
            {
                _totalEnqueuedSpikes++;
                spikeQueue.Enqueue(new Spike
                {
                    Charge = 0xFF,
                    ArrivalTime = t,
                    Target = _energyInjectionNeuron
                }, t);
            }
        }
    }

    private void ProcessPendingSpike(long t, PriorityQueue<Spike, long> spikeQueue, Spike spike)
    {
        var spikeTarget = spike.Target;
        spikeTarget.WasSpiked = true;
        spikeTarget.LastSpikeTime = spike.ArrivalTime;

        switch (spikeTarget.Type)
        {
            case NeuronType.Inhibitory:

                if (spike.Charge <= 0)
                {
                    break; //Inhibitory neurons do not process other inhibitory spikes.
                }

                spikeTarget.Charge += spike.Charge;

                if (spikeTarget.Charge >= 1f)
                {
                    EnqueueSpikes(t, spikeTarget, spikeQueue, isInhibitory: true);
                    //Inhibitory neurons are not assigned to IO buffer roles at Network creation time.
                    spikeTarget.Charge = 0f;
                }

                break;
            case NeuronType.Excitatory:

                spikeTarget.Charge += spike.Charge;
                spikeTarget.Charge = Math.Clamp(spikeTarget.Charge, 0f, 1f); 

                if (spikeTarget.Charge >= 1f)
                {
                    EnqueueSpikes(t, spikeTarget, spikeQueue, isInhibitory: false);
                    HandleIoBufferRoles(t, spikeTarget, spikeQueue);
                    spikeTarget.Charge = 0f;
                }

                break;
        }
    }

    private void HandleIoBufferRoles(long tick, Neuron spikeTarget, PriorityQueue<Spike, long> spikeQueue)
    {
        if (spikeTarget.IOBuffer != null)
        {
            switch (spikeTarget.IOBufferRole)
            {
                //Do nothing in these cases.
                case IOBufferRole.CursorWriteInputA:    //Last spike time already written.
                case IOBufferRole.CursorWriteInputB:
                case IOBufferRole.CursorReadOutput:
                case IOBufferRole.CursorMaxLimitNotifier:
                case IOBufferRole.CursorMinLimitNotifier:
                    break;

                //We should decrement the cursor. If the method returns false, we need to send a limit notification using the Min Limit Notifier mapped role neuron.
                case IOBufferRole.CursorDecrementer:
                    if (spikeTarget.IOBuffer.DecrementCursor())
                    {
                        //throw new NotImplementedException();
                    }
                    else
                    {
                        //throw new NotImplementedException();
                    }
                    break;

                //We should increment the cursor. If the method returns false, we need to send a limit notification using the Max Limit Notifier mapped role neuron.
                case IOBufferRole.CursorIncrementer:
                    if (spikeTarget.IOBuffer.IncrementCursor())
                    {
                        //throw new NotImplementedException();
                    }
                    else
                    {
                        //throw new NotImplementedException();
                    }
                    break;

                case IOBufferRole.CursorRead:
                    var readOutputNeuron = spikeTarget.IOBuffer.AssignedNeurons.First(x => x.IOBufferRole == IOBufferRole.CursorReadOutput);

                    if (readOutputNeuron.Output0 is not null)
                    {
                        var arrivalTime = tick + readOutputNeuron.Output0Delay.Value;
                        var arrivalTimeDelta = tick + readOutputNeuron.Output0Delay.Value + spikeTarget.IOBuffer.ReadCursor().Value;

                        spikeQueue.Enqueue(new Spike
                        {
                            Charge = 1f,
                            ArrivalTime = arrivalTime,
                            Source = readOutputNeuron,
                            Target = readOutputNeuron.Output0
                        }, arrivalTime);

                        spikeQueue.Enqueue(new Spike
                        {
                            Charge = 1f,
                            ArrivalTime = arrivalTimeDelta,
                            Source = readOutputNeuron,
                            Target = readOutputNeuron.Output0
                        }, arrivalTimeDelta);

                        _totalEnqueuedSpikes+=2;
                    }

                    if (readOutputNeuron.Output1 is not null)
                    {
                        var arrivalTime = tick + readOutputNeuron.Output1Delay.Value;
                        var arrivalTimeDelta = tick + readOutputNeuron.Output1Delay.Value + spikeTarget.IOBuffer.ReadCursor().Value;

                        spikeQueue.Enqueue(new Spike
                        {
                            Charge = 1f,
                            ArrivalTime = arrivalTime,
                            Source = readOutputNeuron,
                            Target = readOutputNeuron.Output1
                        }, arrivalTime);

                        spikeQueue.Enqueue(new Spike
                        {
                            Charge = 1f,
                            ArrivalTime = arrivalTimeDelta,
                            Source = readOutputNeuron,
                            Target = readOutputNeuron.Output1
                        }, arrivalTimeDelta);

                        _totalEnqueuedSpikes += 2;
                    }

                    if (readOutputNeuron.Output2 is not null)
                    {
                        var arrivalTime = tick + readOutputNeuron.Output2Delay.Value;
                        var arrivalTimeDelta = tick + readOutputNeuron.Output2Delay.Value + spikeTarget.IOBuffer.ReadCursor().Value;

                        spikeQueue.Enqueue(new Spike
                        {
                            Charge = 1f,
                            ArrivalTime = arrivalTime,
                            Source = readOutputNeuron,
                            Target = readOutputNeuron.Output2
                        }, arrivalTime);

                        spikeQueue.Enqueue(new Spike
                        {
                            Charge = 1f,
                            ArrivalTime = arrivalTimeDelta,
                            Source = readOutputNeuron,
                            Target = readOutputNeuron.Output2
                        }, arrivalTimeDelta);

                        _totalEnqueuedSpikes += 2;
                    }

                    break;

                case IOBufferRole.CursorWrite:
                    var writeInputANeuron = spikeTarget.IOBuffer.AssignedNeurons.First(x => x.IOBufferRole == IOBufferRole.CursorWriteInputA);
                    var writeInputBNeuron = spikeTarget.IOBuffer.AssignedNeurons.First(x => x.IOBufferRole == IOBufferRole.CursorWriteInputB);
                    var tickDelta = Math.Abs(writeInputANeuron.LastSpikeTime - writeInputBNeuron.LastSpikeTime);
                    var value = (byte)(tickDelta % 255);
                    spikeTarget.IOBuffer.WriteCursor(value);
                    break;
            }
        }
    }

    private void EnqueueSpikes(long tick, Neuron spikeTarget, PriorityQueue<Spike, long> spikeQueue, bool isInhibitory)
    {
        if (spikeTarget.Output0 != null)
        {
            var out0ArrivalTime = tick + spikeTarget.Output0Delay.Value;

            spikeQueue.Enqueue(new Spike
            {
                ArrivalTime = out0ArrivalTime,
                Charge = spikeTarget.Output0Weight.Value * (isInhibitory ? -1.0f : 1.0f),
                Source = spikeTarget,
                Target = spikeTarget.Output0
            }, out0ArrivalTime);

            _totalEnqueuedSpikes ++;
        }

        if (spikeTarget.Output1 != null)
        {
            var out1ArrivalTime = tick + spikeTarget.Output1Delay.Value;

            spikeQueue.Enqueue(new Spike
            {
                ArrivalTime = out1ArrivalTime,
                Charge = spikeTarget.Output1Weight.Value * (isInhibitory ? -1.0f : 1.0f),
                Source = spikeTarget,
                Target = spikeTarget.Output1
            }, out1ArrivalTime);

            _totalEnqueuedSpikes++;
        }

        if (spikeTarget.Output2 != null)
        {
            var out2ArrivalTime = tick + spikeTarget.Output2Delay.Value;

            spikeQueue.Enqueue(new Spike
            {
                ArrivalTime = out2ArrivalTime,
                Charge = spikeTarget.Output2Weight.Value * (isInhibitory ? -1.0f : 1.0f),
                Source = spikeTarget,
                Target = spikeTarget.Output2
            }, out2ArrivalTime);

            _totalEnqueuedSpikes++;
        }
    }

    private Neuron NextWellConnectedNonIoNeuron() => 
        _neurons.First(x => 
            x.Type == NeuronType.Excitatory
        &&  x.Inputs.Count > 3 
        &&  x.Output0 != null 
        &&  x.Output1 != null 
        &&  x.Output2 != null 
        &&  x.IOBuffer == null);

    internal void Mutate()
    {
        var neuronIndex = _random.NextInt32(0, _neurons.Count(), _ioBufferExclusions);
        var outputIndex = _random.Next(0, 2);
        var neuron = _neurons[neuronIndex];

        if (outputIndex == 0 && neuron.Output0Weight.HasValue)
        {
            neuron.Output0Weight = _random.NextDouble();
        }
        else if (outputIndex == 1 && neuron.Output1Weight.HasValue)
        {
            neuron.Output1Weight = _random.NextDouble();
        }
        else if (outputIndex == 2 && neuron.Output2Weight.HasValue)
        {
            neuron.Output2Weight = _random.NextDouble();
        }
    }
}
