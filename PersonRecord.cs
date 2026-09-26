namespace OpenFaceRegistry;

public sealed class PersonRecord
{
    public long Id { get; set; }
    public string FullName { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string Notes { get; set; } = "";
    public int SampleCount { get; set; }
}

public sealed record FaceSample(long PersonId, byte[] ImageBytes);
