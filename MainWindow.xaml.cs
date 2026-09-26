using Microsoft.Win32;
using OpenCvSharp;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Media.Animation;

namespace OpenFaceRegistry;

public partial class MainWindow : System.Windows.Window
{
    private const double MatchDistanceLimit = 75;
    private readonly RegistryDatabase _database = new();
    private readonly FaceMatcher _matcher = new();
    private byte[]? _queryBytes;
    private PersonRecord? _candidate;
    private Storyboard? _scanStoryboard;

    public MainWindow() => InitializeComponent();

    private void SelectPhoto_Click(object sender, RoutedEventArgs e)
    {
        var picker = new OpenFileDialog
        {
            Title = "Fotoğraf seç",
            Filter = "Tüm dosyalar (*.*)|*.*|Görüntüler|*.jpg;*.jpeg;*.png;*.bmp;*.tif;*.tiff;*.webp",
            CheckFileExists = true
        };
        if (picker.ShowDialog(this) == true) LoadPhoto(picker.FileName);
    }

    private void PhotoPanel_DragOver(object sender, DragEventArgs e) => e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;

    private void PhotoPanel_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files) LoadPhoto(files[0]);
    }

    private void LoadPhoto(string path)
    {
        try
        {
            var bytes = File.ReadAllBytes(path);
            using var decoded = Cv2.ImDecode(bytes, ImreadModes.Color);
            if (decoded.Empty()) throw new InvalidDataException("Bu dosya OpenCV tarafından okunamadı. JPG veya PNG gibi yaygın bir görüntü biçimi seç.");

            var image = new BitmapImage();
            using (var stream = new MemoryStream(bytes))
            {
                image.BeginInit();
                image.CacheOption = BitmapCacheOption.OnLoad;
                image.StreamSource = stream;
                image.EndInit();
                image.Freeze();
            }

            _queryBytes = bytes;
            _candidate = null;
            QueryImage.Source = image;
            QueryImage.Visibility = Visibility.Visible;
            PhotoPlaceholder.Visibility = Visibility.Collapsed;
            ImageTag.Visibility = Visibility.Visible;
            SelectedFileText.Text = Path.GetFileName(path);
            FileHint.Text = $"{decoded.Width} × {decoded.Height} px";
            RunScanButton.IsEnabled = true;
            ResetCandidate();
            ResultTitle.Text = "Fotoğraf hazır";
            ResultSubtitle.Text = "Eşleştirme başlatmak için düğmeye bas.";
            SetStatus("FOTOĞRAF HAZIR", "#98A2B3");
        }
        catch (Exception ex)
        {
            _queryBytes = null;
            QueryImage.Source = null;
            QueryImage.Visibility = Visibility.Collapsed;
            PhotoPlaceholder.Visibility = Visibility.Visible;
            ImageTag.Visibility = Visibility.Collapsed;
            FileHint.Text = "Henüz fotoğraf seçilmedi";
            RunScanButton.IsEnabled = false;
            MessageBox.Show(ex.Message, "Fotoğraf açılamadı", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void SearchFace_Click(object sender, RoutedEventArgs e)
    {
        if (_queryBytes is null) return;
        byte[] query = _queryBytes;
        try
        {
            RunScanButton.IsEnabled = false;
            ScanButton.IsEnabled = false;
            ResetCandidate();
            ResultTitle.Text = "Fotoğraf taranıyor…";
            ResultSubtitle.Text = "Yerel kayıtlı örnekler karşılaştırılıyor.";
            SetStatus("TARANIYOR", "#A5B4FC");
            StartScanAnimation();

            var samples = _database.GetSamples();
            var result = await Task.Run(() => _matcher.Find(query, samples));
            StopScanAnimation();

            var person = _database.Get(result.PersonId);
            if (person is null || result.Distance > MatchDistanceLimit)
            {
                ResultTitle.Text = "Eşleşme bulunamadı";
                ResultSubtitle.Text = "Kayıtlı fotoğraflarla yeterince yakın bir sonuç yok.";
                CandidateName.Text = "Uygun aday yok";
                CandidateDetails.Text = "Farklı bir fotoğraf deneyebilir veya kayıtlı örnek fotoğrafları güncelleyebilirsin.";
                CandidateDistance.Text = person is null ? "" : $"En yakın adayın uzaklığı: {result.Distance:F1}";
                SetStatus("SONUÇ YOK", "#F79009");
                return;
            }

            _candidate = person;
            CandidateName.Text = person.FullName;
            CandidateDetails.Text = string.Join("\n", new[] { person.Phone, person.Email, person.Notes }.Where(s => !string.IsNullOrWhiteSpace(s)));
            if (string.IsNullOrWhiteSpace(CandidateDetails.Text)) CandidateDetails.Text = "Bu kayıt için ek bilgi girilmemiş.";
            CandidateDistance.Text = $"Eşleşme uzaklığı: {result.Distance:F1} · düşük değer daha yakın";
            ConfirmButton.IsEnabled = true;
            RejectButton.IsEnabled = true;
            ResultTitle.Text = "Olası eşleşme bulundu";
            ResultSubtitle.Text = "Aşağıdaki kaydı gözden geçirip onayla.";
            SetStatus("ADAY BULUNDU", "#12B76A");
        }
        catch (Exception ex)
        {
            StopScanAnimation();
            ResultTitle.Text = "Tarama tamamlanamadı";
            ResultSubtitle.Text = ex.Message;
            CandidateName.Text = "Eşleşme yapılamadı";
            CandidateDetails.Text = ex.Message;
            SetStatus("HATA", "#F04438");
        }
        finally
        {
            RunScanButton.IsEnabled = _queryBytes is not null;
            ScanButton.IsEnabled = true;
        }
    }

    private void StartScanAnimation()
    {
        ScanLine.Visibility = Visibility.Visible;
        ScanGlow.Opacity = 0.4;
        _scanStoryboard = (Storyboard)FindResource("ScanStoryboard");
        _scanStoryboard.Begin(this, true);
    }

    private void StopScanAnimation()
    {
        _scanStoryboard?.Stop(this);
        ScanLine.Visibility = Visibility.Collapsed;
        ScanGlow.Opacity = 0;
    }

    private void SetStatus(string text, string color)
    {
        ScanStatus.Text = text;
        StatusDot.Fill = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString(color)!;
    }

    private void ResetCandidate()
    {
        _candidate = null;
        CandidateName.Text = "Henüz aday yok";
        CandidateDetails.Text = "Eşleşme bulunduğunda bilgiler burada görünür.";
        CandidateDistance.Text = "";
        ConfirmButton.IsEnabled = false;
        RejectButton.IsEnabled = false;
    }

    private void ConfirmMatch_Click(object sender, RoutedEventArgs e)
    {
        if (_candidate is null) return;
        var approved = MessageBox.Show($"'{_candidate.FullName}' kaydını açmak istiyor musun?", "Eşleşmeyi onayla", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (approved == MessageBoxResult.Yes) new PeopleWindow(_database, _candidate.Id) { Owner = this }.ShowDialog();
    }

    private void RejectMatch_Click(object sender, RoutedEventArgs e)
    {
        ResetCandidate();
        ResultTitle.Text = "Eşleşme reddedildi";
        ResultSubtitle.Text = "İstersen başka bir fotoğraf seçip yeniden ara.";
        SetStatus("REDDEDİLDİ", "#F79009");
    }

    private void OpenPeople_Click(object sender, RoutedEventArgs e) => new PeopleWindow(_database) { Owner = this }.ShowDialog();
}
