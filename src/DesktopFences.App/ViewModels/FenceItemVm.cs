using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Media;
using DesktopFences.Core.Models;

namespace DesktopFences.App.ViewModels;

public sealed class FenceItemVm : INotifyPropertyChanged
{
    private bool _isSelected;
    private bool _isDragging;

    public Guid ItemId { get; init; } = Guid.NewGuid();
    public FenceItemKind Kind { get; set; } = FenceItemKind.Stored;
    public required string Name { get; init; }
    public string? StorageName { get; set; }
    public string? Path { get; set; }
    public string? OriginalPath { get; set; }
    public ImageSource? Icon { get; init; }
    public int? OriginalX { get; init; }
    public int? OriginalY { get; init; }

    public string DisplayName =>
        string.IsNullOrWhiteSpace(Name) ? Path ?? "" : Name;

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected == value)
                return;
            _isSelected = value;
            OnPropertyChanged();
        }
    }

    public bool IsDragging
    {
        get => _isDragging;
        set
        {
            if (_isDragging == value)
                return;
            _isDragging = value;
            OnPropertyChanged();
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public FenceItemState ToState() => new()
    {
        ItemId = ItemId,
        Kind = Kind,
        Name = Name,
        StorageName = Kind == FenceItemKind.Stored ? StorageName : null,
        OriginalPath = OriginalPath,
        OriginalX = OriginalX,
        OriginalY = OriginalY
    };

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
