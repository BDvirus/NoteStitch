using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace NoteStitch;

/// <summary>
/// ViewModel wrapping <see cref="NotepadDoc"/> for WinUI 3 data-binding.
/// </summary>
public class NotepadDocVm : INotifyPropertyChanged
{
    private bool _isChecked = true;

    public NotepadDoc Doc { get; }

    public NotepadDocVm(NotepadDoc doc)
    {
        Doc = doc;
    }

    public string Filename     => Doc.Filename;
    public string CharCountText => $"({Doc.CharCount:N0} ch)";

    public bool IsChecked
    {
        get => _isChecked;
        set
        {
            if (_isChecked == value) return;
            _isChecked = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
