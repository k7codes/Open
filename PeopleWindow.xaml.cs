using Microsoft.Win32;
using OpenCvSharp;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace OpenFaceRegistry;

public partial class PeopleWindow : System.Windows.Window
{
    private readonly RegistryDatabase _database;
    private long _selectedId;

    public PeopleWindow(RegistryDatabase database, long selectedId = 0)
    {
        _database = database;
        InitializeComponent();
        RefreshPeople();
        if (selectedId != 0) PeopleGrid.SelectedItem = ((IEnumerable<PersonRecord>)PeopleGrid.ItemsSource).FirstOrDefault(p => p.Id == selectedId);
    }

    private void RefreshPeople(long selectId = 0)
    {
        PeopleGrid.ItemsSource = _database.Search();
        if (selectId != 0) PeopleGrid.SelectedItem = ((IEnumerable<PersonRecord>)PeopleGrid.ItemsSource).FirstOrDefault(p => p.Id == selectId);
    }

    private void PeopleGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PeopleGrid.SelectedItem is not PersonRecord person) return;
        _selectedId = person.Id;
        FormTitle.Text = "Kişi bilgilerini düzenle";
        NameBox.Text = person.FullName;
        PhoneBox.Text = person.Phone;
        EmailBox.Text = person.Email;
        NotesBox.Text = person.Notes;
        SampleLabel.Text = $"Kayıtlı örnek fotoğraf: {person.SampleCount}. Yeni fotoğraflar eklemek için kaydet.";
    }

    private void New_Click(object sender, RoutedEventArgs e)
    {
        _selectedId = 0;
        PeopleGrid.SelectedItem = null;
        NameBox.Clear(); PhoneBox.Clear(); EmailBox.Clear(); NotesBox.Clear();
        FormTitle.Text = "Yeni kişi";
        SampleLabel.Text = "Yeni kişi için en az 2 örnek fotoğraf ekle.";
        NameBox.Focus();
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(NameBox.Text))
        {
            MessageBox.Show("Ad Soyad alanı zorunludur.", "Eksik bilgi", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var picker = new OpenFileDialog
        {
            Title = "Örnek fotoğraf seç (mevcut kişiye yeni örnekler ekleyebilirsin)",
            Filter = "Tüm dosyalar (*.*)|*.*|Görüntüler|*.jpg;*.jpeg;*.png;*.bmp;*.tif;*.tiff;*.webp",
            Multiselect = true
        };
        var samples = new List<byte[]>();
        if (picker.ShowDialog(this) == true)
        {
            foreach (var path in picker.FileNames)
            {
                var bytes = File.ReadAllBytes(path);
                if (bytes.Length > 25 * 1024 * 1024)
                {
                    MessageBox.Show($"'{Path.GetFileName(path)}' 25 MB sınırını aşıyor.", "Fotoğraf çok büyük", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                using var decoded = Cv2.ImDecode(bytes, ImreadModes.Grayscale);
                if (decoded.Empty())
                {
                    MessageBox.Show($"'{Path.GetFileName(path)}' görüntü olarak okunamadı.", "Geçersiz fotoğraf", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                samples.Add(bytes);
            }
        }

        if (_selectedId == 0 && samples.Count < 2)
        {
            MessageBox.Show("Yeni kişi kaydı için en az 2 örnek fotoğraf seç.", "Fotoğraf gerekli", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var record = new PersonRecord { Id = _selectedId, FullName = NameBox.Text.Trim(), Phone = PhoneBox.Text.Trim(), Email = EmailBox.Text.Trim(), Notes = NotesBox.Text.Trim() };
        _selectedId = _database.Save(record, samples);
        RefreshPeople(_selectedId);
        MessageBox.Show("Kişi kaydı kaydedildi.", "Tamamlandı", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Delete_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedId == 0) return;
        var answer = MessageBox.Show("Kişiyi ve kayıtlı fotoğraflarını silmek istiyor musun?", "Kaydı sil", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (answer != MessageBoxResult.Yes) return;
        _database.Delete(_selectedId);
        New_Click(sender, e);
        RefreshPeople();
    }
}
