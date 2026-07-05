using System;
using TestMultiplayer.Data;
using UnityEngine;

namespace TestMultiplayer.Data
{
    [Serializable]
    public class CharacterProfile
    {
        public string PlayerName = "Player";
        public CharacterAppearanceData Appearance = CharacterAppearanceData.Default;

        public CharacterProfile Clone()
        {
            return new CharacterProfile
            {
                PlayerName = PlayerName,
                Appearance = Appearance
            };
        }
    }

    public static class CharacterProfileStore
    {
        private const string PlayerPrefsKey = "TestMultiplayer.CharacterProfile";

        public static CharacterProfile Load()
        {
            string json = PlayerPrefs.GetString(PlayerPrefsKey, string.Empty);

            if (string.IsNullOrWhiteSpace(json))
            {
                return new CharacterProfile();
            }

            try
            {
                CharacterProfile profile = JsonUtility.FromJson<CharacterProfile>(json);
                return Sanitize(profile);
            }
            catch
            {
                return new CharacterProfile();
            }
        }

        public static void Save(CharacterProfile profile)
        {
            CharacterProfile safeProfile = Sanitize(profile);
            PlayerPrefs.SetString(PlayerPrefsKey, JsonUtility.ToJson(safeProfile));
            PlayerPrefs.Save();
        }

        public static CharacterProfile Sanitize(CharacterProfile profile)
        {
            profile ??= new CharacterProfile();
            profile.PlayerName = string.IsNullOrWhiteSpace(profile.PlayerName) ? "Player" : profile.PlayerName.Trim();
            profile.Appearance.Head = Mathf.Max(0, profile.Appearance.Head);
            profile.Appearance.Hair = Mathf.Max(0, profile.Appearance.Hair);
            profile.Appearance.LowerBody = Mathf.Max(0, profile.Appearance.LowerBody);
            profile.Appearance.UpperBody = Mathf.Max(0, profile.Appearance.UpperBody);
            profile.Appearance.Eyes = Mathf.Max(0, profile.Appearance.Eyes);
            return profile;
        }
    }
}
