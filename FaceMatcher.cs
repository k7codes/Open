using OpenCvSharp;
using OpenCvSharp.Face;

namespace OpenFaceRegistry;

public sealed record MatchCandidate(long PersonId, double Distance);

public sealed class FaceMatcher
{
    private const int FaceWidth = 160;
    private const int FaceHeight = 160;

    public MatchCandidate Find(byte[] queryBytes, IReadOnlyList<FaceSample> samples)
    {
        if (samples.Count < 2) throw new InvalidOperationException("Eşleştirme için veritabanında en az iki örnek fotoğraf bulunmalı.");

        var faces = new List<Mat>();
        try
        {
            foreach (var sample in samples) faces.Add(Prepare(sample.ImageBytes));
            using var query = Prepare(queryBytes);
            using var recognizer = LBPHFaceRecognizer.Create();
            recognizer.Train(faces, samples.Select(s => checked((int)s.PersonId)));
            recognizer.Predict(query, out var label, out var distance);
            return new MatchCandidate(label, distance);
        }
        finally
        {
            foreach (var face in faces) face.Dispose();
        }
    }

    private static Mat Prepare(byte[] bytes)
    {
        using var source = Cv2.ImDecode(bytes, ImreadModes.Grayscale);
        if (source.Empty()) throw new InvalidOperationException("Görüntü okunamadı. JPG veya PNG dosyası seç.");
        var face = new Mat();
        Cv2.Resize(source, face, new Size(FaceWidth, FaceHeight));
        Cv2.EqualizeHist(face, face);
        return face;
    }
}
