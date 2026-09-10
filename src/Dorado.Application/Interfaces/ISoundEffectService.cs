namespace Dorado.Application.Interfaces;

public interface ISoundEffectService
{
    bool SoundEffectsEnabled { get; set; }
    void PlaySyncComplete();
    void PlayDownloadComplete();
    void PlayRipComplete();
    void PlayBurnComplete();
    void PlayNotification();
}
