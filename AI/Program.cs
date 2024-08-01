//
using AI;
using System.Text;
using System.Xml;

var outputBuffer = new IOBuffer(26, IOBufferAccess.ReadWrite, true);
var random = new LcgRandom(111);
var neuronCount = 501;

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

for (int x = 0; x < 50; x++)
{
    var clone = new Network(network);
    clone.Mutate();
    top100Networks.Add(clone, double.MaxValue);
}

double lastScore = double.MaxValue;
var stuckCount = 0;
bool activityBootstrapped = false;

while (true)
{
    Parallel.ForEach(top100Networks, (network) =>
    {
        network.Key.Simulate(1_000_000_000, 1_000_000);
        var outputBuffer = network.Key.IOBuffers.First(x => x.Access == IOBufferAccess.ReadWrite);
        var inputBuffer = network.Key.IOBuffers.First(x => x.Access == IOBufferAccess.Read);
        var output = network.Key.IOBuffers.First(x => x.Access == IOBufferAccess.ReadWrite).Buffer;
        //Console.WriteLine(Encoding.UTF8.GetString(output));
        var outputMSE = Scoring.CalculateMSE(expectedOutput, output);
        var outputLev = Scoring.ComputeLevenshteinDistance(expectedOutput, output);
        var rcr = inputBuffer.ReadCoverageRatio();
        var wcr = outputBuffer.WriteCoverageRatio();

        if (network.Key.SpikedPercentage < 0.80f && activityBootstrapped == false)
        {
            network.Key.Note = "Activity Bootstrapping (80%)";
            top100Networks[network.Key] = 1_000_000_000 - (int)(1_000_000_000 * (network.Key.SpikedPercentage));
        }
        else if (rcr < 1.0f)
        {
            network.Key.Note = "IO: Read constrained";
            top100Networks[network.Key] = 100_000_000 - (int)(100_000_000 * rcr) + 10_000_000;
        }
        else if (inputBuffer.FinalReadTick > outputBuffer.FirstWriteTick)
        {
            activityBootstrapped = true;
            network.Key.Note = "IO: Write before Read constrained";
            top100Networks[network.Key] = 1_000_000 * ((double)inputBuffer.FinalReadTick.Value / outputBuffer.FirstWriteTick.Value);
        }
        //else if (outputBuffer.FirstWriteTick == null)
        //{
        //    network.Key.Note = "IO: Write before Read constrained (no write yet)";
        //    top100Networks[network.Key] = 10_000_000;
        //}
        else if (wcr < 1.0f)
        {
            network.Key.Note = "IO: Write constrained";
            top100Networks[network.Key] = 1_000_000 - (int)(1_000_000 * wcr);
        }
        else
        {
            network.Key.Note = "Correctness";
            top100Networks[network.Key] = outputMSE + outputLev;            
        }
    });

    List<KeyValuePair<Network, double>> top10;

    if (stuckCount > 4)
    {
        top10 = top100Networks.OrderBy(x => x.Value).Where(x=>x.Value < double.MaxValue).TakeLast(50).ToList();
    }
    else
    {
        top10 = top100Networks.OrderBy(x => x.Value).Take(50).ToList();
    }
    
    var bestOutput = top10[0].Key.IOBuffers.First(x => x.Access == IOBufferAccess.ReadWrite).Buffer;

    Console.WriteLine("Best Score - " + top10[0].Value  + " - " + top10[0].Key.Note + " - " + top10[0].Key.SpikedPercentage +" - " + Encoding.UTF8.GetString(bestOutput));

    if (top10[0].Value == lastScore)
    {
        stuckCount++;
    }
    else
    {
        lastScore = top10[0].Value;
        stuckCount = 0;
    }

    top100Networks.Clear();

    foreach (var n in top10)
    {
        var existing = new Network(n.Key);
        top100Networks[existing] = double.MaxValue;

        for(int x = 0; x < 5 /** (stuckCount + 1)*/; x++)
        {
            var clone = new Network(n.Key);

            for (int y = 0; y < x; y++)
            {
                clone.Mutate();
            }
            
            if (x == 0)
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