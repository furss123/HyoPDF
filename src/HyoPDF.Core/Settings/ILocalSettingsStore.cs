namespace HyoPDF.Core.Settings;

public interface ILocalSettingsStore
{
    AppSettings Load();
    void Save(AppSettings settings);
}
