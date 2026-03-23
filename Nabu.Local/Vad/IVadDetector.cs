namespace Nabu.Local.Vad;

public interface IVadDetector
{
    int WindowSize { get; }
    float Process(float[] buffer);
    void Reset();
}
