namespace MicBoost.Audio.Settings;

public interface ISettingsService
{
    AppSettings Load();

    void Save(AppSettings settings);
}
