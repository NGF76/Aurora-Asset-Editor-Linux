using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AuroraAssetEditorLinux.Models
{
    public class Game : INotifyPropertyChanged
    {
        private string _title = string.Empty;
        private string _titleId = string.Empty;
        private string _dbId = string.Empty;
        private bool _isGameSelected;

        public string Title
        {
            get => _title;
            set { _title = value; OnPropertyChanged(); }
        }

        public string TitleId
        {
            get => _titleId;
            set { _titleId = value; OnPropertyChanged(); }
        }

        public string DbId
        {
            get => _dbId;
            set { _dbId = value; OnPropertyChanged(); }
        }

        public bool IsGameSelected
        {
            get => _isGameSelected;
            set { _isGameSelected = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
