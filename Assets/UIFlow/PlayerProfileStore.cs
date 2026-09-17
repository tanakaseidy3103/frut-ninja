using System;
using System.IO;
using UnityEngine;

[Serializable]
public class PlayerProfileData
{
    public string nickname = "Player";
    public CharacterSourceType characterSource = CharacterSourceType.Preset;
    public ControlTargetType controlTarget = ControlTargetType.Hand;
    public int presetCharacterIndex;
    public string drawingImagePath = string.Empty;
}

public enum CharacterSourceType
{
    Preset = 0,
    Drawing = 1,
}

public enum ControlTargetType
{
    Hand = 0,
    Character = 1,
}

public static class PlayerProfileStore
{
    private const string FileName = "player_profile.json";
    private static PlayerProfileData cachedProfile;

    private static string ProfilePath => Path.Combine(Application.persistentDataPath, FileName);

    public static PlayerProfileData Get()
    {
        if (cachedProfile != null)
        {
            return cachedProfile;
        }

        if (!File.Exists(ProfilePath))
        {
            cachedProfile = new PlayerProfileData();
            return cachedProfile;
        }

        try
        {
            string json = File.ReadAllText(ProfilePath);
            cachedProfile = JsonUtility.FromJson<PlayerProfileData>(json);
            if (cachedProfile == null)
            {
                cachedProfile = new PlayerProfileData();
            }
        }
        catch
        {
            cachedProfile = new PlayerProfileData();
        }

        return cachedProfile;
    }

    public static void Save(PlayerProfileData profile)
    {
        cachedProfile = profile ?? new PlayerProfileData();
        string json = JsonUtility.ToJson(cachedProfile, true);
        File.WriteAllText(ProfilePath, json);
    }

    public static string GetProfilePath()
    {
        return ProfilePath;
    }
}
