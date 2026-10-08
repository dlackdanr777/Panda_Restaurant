#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

public sealed class FairySoundStage1EvidenceTests
{
    [TestCase(1)]
    [TestCase(11)]
    public void Stage1_ConfirmedBirthRecordsOriginalClipsAndFrameSynchronousEvents(int count)
    {
        using (var host = new EnhancementFairyStage1Host())
            host.RecordSoundBirth(count);
    }
}

internal sealed partial class EnhancementFairyStage1Host
{
    internal void RecordSoundBirth(int count)
    {
        string previousDirectory = EvidenceDirectory;
        string output = Path.Combine("Logs/Phase5Sound_20261002", count == 1 ? "fairy-single" : "fairy-batch");
        EvidenceDirectory = output;
        const int fps = 20;
        int frames = count == 1 ? 110 : 240;
        var sources = new List<AudioSource>();
        for (int i = 0; i < 10; i++) sources.Add(_services.AddComponent<AudioSource>());
        Set(_sound, "_effectAudioDic", new Dictionary<EffectType, List<AudioSource>> { [EffectType.None] = sources });
        try
        {
            Move(ERestaurantFloorType.Floor3);
            var items = Catalog.Where(EnhancementFairyCatalog.IsEligible).Take(count).ToArray();
            Restore(items.Select(item => new KeyValuePair<string, int>(item.Id, 1)));
            using (var audio = new Phase5AudioEvidence(_sound, output, fps))
            {
                foreach (var item in items) EnhancementFairyAcquisitionEvents.PublishConfirmed("sound-evidence-" + count, item.Id);
                for (int frame = 0; frame < frames; frame++)
                {
                    audio.BeginFrame(frame);
                    Habitat.AdvancePreview(1f / fps);
                    Capture(Path.Combine("frames", frame.ToString("D5") + ".png"));
                }
                audio.Save(frames / (double)fps);
                Assert.That(audio.EventCount, Is.GreaterThanOrEqualTo(2));
                Assert.That(EnhancementFairyAcquisitionEvents.Queue.PendingCount, Is.Zero);
            }
        }
        finally { EvidenceDirectory = previousDirectory; }
    }
}
#endif
