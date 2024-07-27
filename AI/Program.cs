//
using AI;

var network = new Network(
    neuronCount: 100_000,
    ioBuffers: new List<IOBuffer>
    {
        new IOBuffer("hello world!", IOBufferAccess.Read, false)
        //...
    });

Console.WriteLine(network.Statistics());

network.Simulate(100_000_000);