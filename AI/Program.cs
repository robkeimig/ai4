//
using AI;
using System.Text;

var outputBuffer = new IOBuffer(26, IOBufferAccess.ReadWrite, true);
var random = new LcgRandom(111);
var neuronCount = 2_644;

var network = new Network(
    neuronCount: neuronCount,
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

while (true)
{
    Parallel.ForEach(top100Networks, (network) =>
    {
        network.Key.Simulate(1_000_000_000, 5_000_000);
        var outputBuffer = network.Key.IOBuffers.First(x => x.Access == IOBufferAccess.ReadWrite);
        var inputBuffer = network.Key.IOBuffers.First(x => x.Access == IOBufferAccess.Read);
        var output = network.Key.IOBuffers.First(x => x.Access == IOBufferAccess.ReadWrite).Buffer;
        Console.WriteLine(Encoding.UTF8.GetString(output));
        var outputMSE = Scoring.CalculateMSE(expectedOutput, output);
        var outputLev = Scoring.ComputeLevenshteinDistance(expectedOutput, output);
        var rcr = inputBuffer.ReadCoverageRatio();
        var wcr = outputBuffer.WriteCoverageRatio();

        if (rcr < 1.0f)
        {
            top100Networks[network.Key] = 1_000_000_000 - (int)(1_000_000_000 * rcr);
        }
        else if (wcr < 1.0f)
        {
            top100Networks[network.Key] = 1_000_000 - (int)(1_000_000 * wcr);
        }
        else
        {
            top100Networks[network.Key] += outputMSE + outputLev;
        }
    });

    var top10 = top100Networks.OrderBy(x => x.Value).Take(10).ToList();

    Console.WriteLine("Best Score: " + top10[0].Value);
    top100Networks.Clear();

    foreach (var n in top10)
    {
        var existing = new Network(n.Key);
        top100Networks[existing] = double.MaxValue;

        for(int x = 0; x < 10; x++)
        {
            var clone = new Network(n.Key);
            clone.Mutate();

            if (x < 5)
            {
                for (int y = 0; y < x; y++)
                {
                    clone.Mutate();
                }
            }
            else
            {
                for (int y = 0; y < neuronCount / 10; y++)
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