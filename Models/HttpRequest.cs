using System.ComponentModel;

namespace ScanerServer.Models
{
    public class HttpRequest : INotifyPropertyChanged
    {
        private int _id;
        private string _method = string.Empty;
        private string _path = string.Empty;
        private string _headers = string.Empty;
        private string _body = string.Empty;
        private DateTime _timestamp;
        private string _clientIp = string.Empty;
        private bool _isCopied;
        private string _type = string.Empty;

        public int Id
        {
            get => _id;
            set
            {
                _id = value;
                OnPropertyChanged(nameof(Id));
            }
        }

        public string Method
        {
            get => _method;
            set
            {
                _method = value;
                OnPropertyChanged(nameof(Method));
            }
        }

        public string Path
        {
            get => _path;
            set
            {
                _path = value;
                OnPropertyChanged(nameof(Path));
            }
        }

        public string Headers
        {
            get => _headers;
            set
            {
                _headers = value;
                OnPropertyChanged(nameof(Headers));
            }
        }

        public string Body
        {
            get => _body;
            set
            {
                _body = value;
                OnPropertyChanged(nameof(Body));
            }
        }

        public DateTime Timestamp
        {
            get => _timestamp;
            set
            {
                _timestamp = value;
                OnPropertyChanged(nameof(Timestamp));
            }
        }

        public string ClientIp
        {
            get => _clientIp;
            set
            {
                _clientIp = value;
                OnPropertyChanged(nameof(ClientIp));
            }
        }

        public bool IsCopied
        {
            get => _isCopied;
            set
            {
                _isCopied = value;
                OnPropertyChanged(nameof(IsCopied));
            }
        }

        public string Type
        {
            get => _type;
            set
            {
                _type = value;
                OnPropertyChanged(nameof(Type));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
