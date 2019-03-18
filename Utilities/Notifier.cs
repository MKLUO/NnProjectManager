using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

#nullable enable

namespace NnManager {
    using RPath = Util.RestrictedPath;

    // FIXME: Implement log chaining here

    public class Notifier {
        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual Dictionary<string, List<string>>? Derivatives { get; }

        protected void Subscribe(INotifyPropertyChanged target) =>
            target.PropertyChanged += OnComponentPropertyChanged;

        protected void OnPropertyChanged(string str) {
            OnPropertyChanged(new PropertyChangedEventArgs(str));
            
            if (Derivatives?.ContainsKey(str) ?? false)
                foreach (var derivative in Derivatives?[str] ?? (new List<string>()))
                    OnPropertyChanged(derivative);
        }

        protected void OnPropertyChanged(PropertyChangedEventArgs e) {            
            PropertyChanged?.Invoke(this, e);
        }

        protected void OnComponentPropertyChanged(object sender, PropertyChangedEventArgs e) {
            OnPropertyChanged(e.PropertyName);
        }

        protected bool SetField<T>(
            ref T field, 
            T value, 
            [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            if (propertyName != null)
                OnPropertyChanged(propertyName);
            return true;
        }
    }
}