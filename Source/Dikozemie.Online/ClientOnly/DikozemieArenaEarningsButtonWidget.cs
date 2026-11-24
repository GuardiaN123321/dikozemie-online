using TaleWorlds.GauntletUI;
using TaleWorlds.GauntletUI.BaseTypes;
using TaleWorlds.Library;

namespace Dikozemie.Online
{
    // «Игрокам это нужно прямо доходчиво объяснить, как заработать деньги».
    // Lives in the sections strip, which belongs to three different screens.
    public sealed class DikozemieArenaEarningsButtonWidget : ButtonWidget
    {
        public DikozemieArenaEarningsButtonWidget(UIContext context)
            : base(context)
        {
        }

        protected override void HandleClick()
        {
            base.HandleClick();
            InformationManager.ShowInquiry(
                new InquiryData(
                    "КАК ЗАРАБОТАТЬ",
                    DikozemieArenaEarnings.Text,
                    true,
                    false,
                    "ПОНЯТНО",
                    string.Empty,
                    null,
                    null),
                true,
                true);
        }
    }

    internal static class DikozemieArenaEarnings
    {
        // The numbers come from the rules the server and the wallet really use.
        internal static string Text =>
            "АРЕНА\n" +
            "За каждого убитого: +" + DikozemieArenaRatingStore.VictoryRatingReward +
            " к рейтингу и столько же монет.\n" +
            "Чем выше его рейтинг, тем больше: ещё +1 за каждые " +
            DikozemieArenaRatingStore.HigherOpponentRewardStep + " очков разницы. " +
            "Лорды наверху таблицы стоят дороже всех.\n" +
            "Пеший убил всадника в открытом бою: ×2.\n" +
            "Ближним ударом убил стрелка в смешанном бою: ×2.\n" +
            "Победа команды: +" + DikozemieArenaTeamRules.VictoryCoinReward +
            " монета, больше против сильной команды.\n" +
            "Поражение: от -1 монеты в малых боях до -" +
            DikozemieArenaTeamRules.GetLossCoinPenalty(DikozemieArenaTeamRules.MaximumTeamSize) +
            " в бою 100×100.\n\n" +
            "МОРСКОЙ БОЙ\n" +
            "За каждого убитого: +" + DikozemieNavalRewardRules.PersonalKillReward +
            " к рейтингу и " + DikozemieNavalRewardRules.PersonalKillReward + " монеты.\n" +
            "Победа: +1 к рейтингу и монеты проигравших делятся между победителями: +" +
            DikozemieNavalRewardRules.LightLossCoinPenalty + " на лёгком корабле, +" +
            DikozemieNavalRewardRules.MediumLossCoinPenalty + " на среднем, +" +
            DikozemieNavalRewardRules.HeavyLossCoinPenalty + " на тяжёлом.\n" +
            "Поражение: -" + DikozemieNavalRewardRules.LightLossCoinPenalty + " монет на лёгком корабле, -" +
            DikozemieNavalRewardRules.MediumLossCoinPenalty + " на среднем, -" +
            DikozemieNavalRewardRules.HeavyLossCoinPenalty + " на тяжёлом.\n\n" +
            "НА ДОРОГЕ\n" +
            "Встречи на карте приносят монеты, засады ведут на арену.";
    }
}
