using System;
using TMPro;
using UnityEngine;

namespace Thuong.Online
{
    public sealed partial class ThuongOnlineSession
    {
        private GameObject villageRulesPanel;
        private readonly TMP_Text[] villageRuleValues = new TMP_Text[8];

        private void OpenVillageRulesSettings()
        {
            if (!CanConfigureRules || draftRules == null || rulesSettingsRoot == null) return;
            if (villageRulesPanel == null)
            {
                var shade = LobbyPanel("Village rules shade", rulesSettingsRoot.transform, Vector2.zero, Vector2.one, false);
                shade.GetComponent<UnityEngine.UI.Image>().color = new Color(0, 0, 0, .92f);
                villageRulesPanel = shade.gameObject;
                var card = LobbyPanel("Village rules card", shade, new Vector2(.18f, .07f), new Vector2(.82f, .93f));
                LobbyText("Village rules title", card, "LUẬT LÀNG VÀ VẬT PHẨM", new Vector2(.04f, .90f), new Vector2(.96f, .98f), 27, TextAlignmentOptions.Center);
                string[] names = { "Số cuộc họp / phòng", "Chờ giữa hai cuộc họp", "Hồi phá hoại", "Thời gian mất điện", "Thời gian hỏng trạm", "Thời gian chặn đường", "Hiệu lực đèn lồng", "Hiệu lực bùa bảo vệ" };
                for (int index = 0; index < names.Length; index++)
                {
                    int captured = index; float y = .80f - index * .082f;
                    var label = LobbyText("Village rule " + index, card, names[index], new Vector2(.05f, y), new Vector2(.57f, y + .07f), 20);
                    label.enableAutoSizing = true; label.fontSizeMin = 14;
                    LobbyButton("Village rule less " + index, card, "−", new Vector2(.59f, y), new Vector2(.68f, y + .07f), () => ChangeVillageRule(captured, -1));
                    villageRuleValues[index] = LobbyText("Village rule value " + index, card, "", new Vector2(.69f, y), new Vector2(.84f, y + .07f), 21, TextAlignmentOptions.Center);
                    LobbyButton("Village rule more " + index, card, "+", new Vector2(.85f, y), new Vector2(.94f, y + .07f), () => ChangeVillageRule(captured, 1));
                }
                var help = LobbyText("Village rule help", card, "0 cuộc họp: tắt chuông và còi. Mỗi người vẫn có tối đa một lượt.\nBấm ÁP DỤNG ở bảng thiết lập ván để lưu cho cả phòng.", new Vector2(.05f, .085f), new Vector2(.95f, .18f), 16, TextAlignmentOptions.Center);
                help.textWrappingMode = TextWrappingModes.Normal;
                LobbyButton("Village rules defaults", card, "MẶC ĐỊNH", new Vector2(.06f, .02f), new Vector2(.47f, .085f), () => { draftRules.village = new ThuongVillageSettings(); RefreshVillageRuleValues(); });
                LobbyButton("Village rules back", card, "QUAY LẠI", new Vector2(.53f, .02f), new Vector2(.94f, .085f), () => villageRulesPanel.SetActive(false));
            }
            villageRulesPanel.SetActive(true); RefreshVillageRuleValues();
        }

        private void ChangeVillageRule(int index, int direction)
        {
            if (!CanConfigureRules || draftRules?.village == null) return;
            var value = draftRules.village;
            int step = direction * 5;
            switch (index)
            {
                case 0: value.meetingLimit = Math.Clamp(value.meetingLimit + direction, 0, 6); break;
                case 1: value.meetingCooldown = Math.Clamp(value.meetingCooldown + step, 15, 120); break;
                case 2: value.sabotageCooldown = Math.Clamp(value.sabotageCooldown + step, 15, 120); break;
                case 3: value.blackoutSeconds = Math.Clamp(value.blackoutSeconds + step, 5, 45); break;
                case 4: value.jamSeconds = Math.Clamp(value.jamSeconds + step, 5, 45); break;
                case 5: value.barricadeSeconds = Math.Clamp(value.barricadeSeconds + direction, 3, 15); break;
                case 6: value.lanternSeconds = Math.Clamp(value.lanternSeconds + step, 5, 60); break;
                case 7: value.wardSeconds = Math.Clamp(value.wardSeconds + step, 5, 60); break;
            }
            RefreshVillageRuleValues();
        }

        private void RefreshVillageRuleValues()
        {
            var value = draftRules.village;
            int[] values = { value.meetingLimit, value.meetingCooldown, value.sabotageCooldown, value.blackoutSeconds,
                value.jamSeconds, value.barricadeSeconds, value.lanternSeconds, value.wardSeconds };
            for (int index = 0; index < values.Length; index++) villageRuleValues[index].text = values[index] + (index == 0 ? " lượt" : "s");
        }
    }
}
