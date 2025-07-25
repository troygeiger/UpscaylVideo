using System;
using System.Diagnostics;
using System.Numerics;

namespace UpscaylVideo.Helpers;

public class AverageProvider<T> where T : notnull, INumberBase<T>, IDivisionOperators<T, T, T>, IConvertible
{
    private T[] _values;
    private readonly T _length;
    private T _lastAverage = default!;
    private int _next = 0;
    private Stopwatch _lastAverageUpdate;

    public AverageProvider() : this(10)
    {
    }
    
    public AverageProvider(int averageSize)
    {
        _values = new T[averageSize];
        _length = (T)Convert.ChangeType(_values.Length, typeof(T));
        _lastAverageUpdate = Stopwatch.StartNew();
    }

    public void Push(T value)
    {
        _values[_next] = value;
        _next = (_next + 1) % _values.Length;
        if (_next == 0)
        {
            UpdateAverage();
        }
    }
    
    public bool AverageReady { get; private set; }

    public T AverageValue => _lastAverage;

    public T GetAverage(bool resetReady)
    {
        if (resetReady)
            AverageReady = false;
        return _lastAverage;
    }
    
    public T GetAverage() => GetAverage(false);
    
    private T UpdateAverage()
    {
        T total = default(T)!;

        for (int i = 0; i < _values.Length; i++)
        {
            total = total + _values[i];
        }
        _lastAverage = total / _length;
        AverageReady = true;
        _lastAverageUpdate.Restart();
        return _lastAverage;
    }

    public void Reset()
    {
        _next = 0;
        for (int i = 0; i < _values.Length; i++)
            _values[i] = default!;
        AverageReady = false;
        _lastAverageUpdate.Reset();
    }
    
    public TimeSpan TimeSinceLastAverageUpdate => _lastAverageUpdate.Elapsed;
}