using System.Text;

namespace AI;

//TODO: Log time of completing all reads. Log time of starting first write.
//At scoring time, determine if we started writing before we read everything.
//If we violate this rule, add a large penalty scaling with the tick overlap.
public class IOBuffer
{
    readonly byte[] _buffer;
    readonly IOBufferAccess _access;
    readonly bool _clearUponClone;
    readonly List<Neuron> _assignedNeurons;
    readonly int[] _bufferReadCount;
    readonly int[] _bufferWriteCount;
    long? _bufferFirstReadTick;
    long? _bufferFirstWriteTick;
    long? _bufferFinalByteReadTick;
    int _cursor = 0;

    public IOBuffer(int size, IOBufferAccess access, bool clearUponClone) 
    {
        _assignedNeurons = new List<Neuron>();
        _bufferReadCount = new int[size];
        _bufferWriteCount = new int[size];
        _buffer = new byte[size];
        _access = access;
        _clearUponClone = clearUponClone;
    }

    public IOBuffer(string content, IOBufferAccess access, bool clearUponClone)
    {
        _assignedNeurons = new List<Neuron>();
        _buffer = Encoding.UTF8.GetBytes(content);
        _bufferReadCount = new int[_buffer.Length];
        _bufferWriteCount = new int[_buffer.Length];
        _access = access;
        _clearUponClone = clearUponClone;
    }

    public IOBuffer(byte[] content, IOBufferAccess access, bool clearUponClone)
    {
        _assignedNeurons = new List<Neuron>();
        _buffer = content;
        _bufferReadCount = new int[_buffer.Length];
        _bufferWriteCount = new int[_buffer.Length];
        _access = access;
        _clearUponClone = clearUponClone;
    }

    public IOBuffer(IOBuffer iOBuffer, Dictionary<Neuron, Neuron> neuronCloneMap)
    {
        _assignedNeurons = iOBuffer._assignedNeurons.Select(an => neuronCloneMap[an]).ToList();
        _buffer = new byte[iOBuffer._buffer.Length];
        _bufferReadCount = new int[_buffer.Length];
        _bufferWriteCount = new int[_buffer.Length];
        _access = iOBuffer.Access;
        _clearUponClone = iOBuffer.ClearUponClone;

        if (!_clearUponClone)
        {
            Array.Copy(iOBuffer._buffer, _buffer, iOBuffer._buffer.Length);
        }
    }

    public double ReadCoverageRatio()
    {
        var total = _buffer.Length;
        var coverage = _bufferReadCount.Count(x => x > 0);
        return (double)coverage / (double)total;
    }

    public double WriteCoverageRatio()
    {
        var total = _buffer.Length;
        var coverage = _bufferWriteCount.Count(x => x > 0);
        return (double)coverage / (double)total;
    }

    public byte[] Buffer => _buffer;

    public IOBufferAccess Access => _access;

    public bool ClearUponClone => _clearUponClone;

    public List<Neuron> AssignedNeurons => _assignedNeurons;

    public long? FirstWriteTick => _bufferFirstWriteTick;
    public long? FirstReadTick => _bufferFirstReadTick;
    public long? FinalReadTick => _bufferFinalByteReadTick;

    public void Clear()
    {
        Array.Clear(_buffer);
    }

    public bool IncrementCursor()
    {
        if (_cursor < _buffer.Length-1)
        {
            _cursor++;
            return true;
        }

        return false;
    }

    public bool DecrementCursor()
    {
        if (_cursor > 0)
        {
            _cursor--;
            return true;
        }

        return false;
    }

    public byte? ReadCursor(long t)
    {
        //Console.WriteLine($"Reading Buffer [{_cursor}]: {_buffer[_cursor]}");
        if (_access == IOBufferAccess.Write) { return null; }
        _bufferReadCount[_cursor]++;

        if (_bufferFirstReadTick == null)
        {
            _bufferFirstReadTick = t; 
        }
        else if (_bufferFinalByteReadTick == null && _bufferReadCount.All(x => x > 0))
        {
            _bufferFinalByteReadTick = t;
        }

        return _buffer[_cursor];
    }

    public void WriteCursor(long t, byte value)
    {
        //Console.WriteLine($"Writing Buffer [{_cursor}]: {value}");
        if (_access == IOBufferAccess.Read) { return; }
        _bufferWriteCount[_cursor]++;

        if (_bufferFirstWriteTick == null)
        {
            _bufferFirstWriteTick = t;
        }

        _buffer[_cursor] = value;
    }
}
