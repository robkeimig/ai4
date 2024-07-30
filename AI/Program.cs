//
using AI;
using System.Text;

var outputBuffer = new IOBuffer(26, IOBufferAccess.ReadWrite, true);

var network = new Network(
    neuronCount: 5500,
    ioBuffers: new List<IOBuffer>
    {
        new IOBuffer("Hello! Can you respond with 'ABCDEFGHIJKLMNOPQRSTUVWXYZ' please?", IOBufferAccess.Read, false),
        outputBuffer
    });

Console.WriteLine(network.Statistics());

var top100Networks = new Dictionary<Network, double>();
var expectedOutput = Encoding.UTF8.GetBytes("ABCDEFGHIJKLMNOPQRSTUVWXYZ");

for (int x = 0; x < 10; x++)
{
    var clone = new Network(network);
    clone.Mutate();
    top100Networks.Add(clone, double.MaxValue);
}

var hasReadWriteMastery = false;

while (true)
{
    hasReadWriteMastery = true;

    Parallel.ForEach(top100Networks, (network) =>
    {
        if (hasReadWriteMastery)
        {
            network.Key.Simulate(10_000_000);
        }
        else
        {
            network.Key.Simulate(1_000_000);
        }
        
        var outputBuffer = network.Key.IOBuffers.First(x => x.Access == IOBufferAccess.ReadWrite);
        var inputBuffer = network.Key.IOBuffers.First(x => x.Access == IOBufferAccess.Read);
        var output = network.Key.IOBuffers.First(x => x.Access == IOBufferAccess.ReadWrite).Buffer;
        Console.WriteLine(Encoding.UTF8.GetString(output));
        var outputMSE = Scoring.CalculateMSE(expectedOutput, output);
        var rcr = inputBuffer.ReadCoverageRatio();
        var wcr = outputBuffer.WriteCoverageRatio();

        top100Networks[network.Key] =
            1_000_000 - (int)(1_000_000 * wcr) +
            1_000_000_000 - (int)(1_000_000_000 * rcr);

        if (top100Networks[network.Key] < 100)
        {
            top100Networks[network.Key] += outputMSE;
        }
        else
        {
            hasReadWriteMastery = false;
        }
    });

    var top10 = top100Networks.OrderBy(x => x.Value).Take(10).ToList();

    Console.WriteLine("Best Score: " + top10[0].Value);
    top100Networks.Clear();

    foreach (var n in top10)
    {
        var existing = new Network(n.Key);
        top100Networks[existing] = double.MaxValue;

        for(int x = 0; x < 2; x++)
        {
            var clone = new Network(n.Key);
            clone.Mutate();

            if (!hasReadWriteMastery)
            {
                for (int y = 0; y < 100; y++)
                {
                    clone.Mutate();
                }
            }

            top100Networks[clone] = double.MaxValue;
        }
    }
}

//    network.Simulate(10_000_000);
//Console.WriteLine(network.TotalSpikes);
//Console.WriteLine(network.SpikedPercentage);