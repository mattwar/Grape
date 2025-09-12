using System;

namespace SDL3.Model;

public class Properties : IDisposable
{
    private uint _propertiesId;

    internal Properties(uint propertiesId)
    {
        _propertiesId = propertiesId;
    }

    public static readonly Properties Empty = new Properties(0);

    public SDL.PropertyType GetPropertyType(string propertyName)
    {
        if (_propertiesId == 0)
            return SDL.PropertyType.Invalid;
        return SDL.GetPropertyType(_propertiesId, propertyName);
    }

    public object? GetPropertyValue(string propertyName)
    {
        if (_propertiesId == 0)    
            return null;

        switch (GetPropertyType(propertyName))
        {
            case SDL.PropertyType.String:
                return SDL.GetStringProperty(_propertiesId, propertyName, "");
            case SDL.PropertyType.Number:
                return SDL.GetNumberProperty(_propertiesId, propertyName, 0);
            case SDL.PropertyType.Boolean:
                return SDL.GetBooleanProperty(_propertiesId, propertyName, false);
            case SDL.PropertyType.Float:
                return SDL.GetFloatProperty(_propertiesId, propertyName, 0.0f);
            default:
                return null;
        }
    }

    public string GetStringProperty(string propertyName)
    {
        if (_propertiesId == 0
            || GetPropertyType(propertyName) != SDL.PropertyType.String)
            return "";
        return SDL.GetStringProperty(_propertiesId, propertyName, "");
    }

    public long GetNumberProperty(string propertyName)
    {
        if (_propertiesId == 0
            || GetPropertyType(propertyName) != SDL.PropertyType.Number)
            return 0;
        return SDL.GetNumberProperty(_propertiesId, propertyName, 0);
    }

    public float GetFloatProperty(string propertyName)
    {
        if (_propertiesId == 0
            || GetPropertyType(propertyName) != SDL.PropertyType.Float)
            return 0;
        return SDL.GetFloatProperty(_propertiesId, propertyName, 0);
    }

    public float GetBooleanProperty(string propertyName)
    {
        if (_propertiesId == 0
            || GetPropertyType(propertyName) != SDL.PropertyType.Boolean)
            return 0;
        return SDL.GetFloatProperty(_propertiesId, propertyName, 0);
    }

    public Span<T> GetSpanProperty<T>(string propertyName, T end) where T : unmanaged =>
        GetSpanProperty<T>(propertyName, (T item) => EqualityComparer<T>.Default.Equals(item, end));

    public Span<T> GetSpanProperty<T>(string propertyName, Func<T, bool> fnIsEnd) where T : unmanaged
    {
        if (_propertiesId == 0
            || GetPropertyType(propertyName) != SDL.PropertyType.Pointer)
            return Span<T>.Empty;

        unsafe
        {
            T* start = (T*)SDL.GetPointerProperty(_propertiesId, propertyName, 0);

            // determine length of the array
            T* current = start;
            int count = 0;
            while (!fnIsEnd(*current))
            {
                count++;
                current++;
            }

            return new Span<T>(start, count);
        }
    }

    public void Dispose()
    {
        if (_propertiesId != 0)
        {
            var id = Interlocked.Exchange(ref _propertiesId, 0);
            if (id != 0)
            {
                SDL.DestroyProperties(id);
            }
        }
    }
}