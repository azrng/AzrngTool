using AzrngTools.Utils;
using AzrngTools.Utils.Events;
using Avalonia.Controls;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Text;

namespace AzrngTools.ViewModels.Encode;

public partial class ChineseConvertPageViewModel : ViewModelBase
{
    private readonly IMessageService _messageService;

    public ChineseConvertPageViewModel(IMessageService messageService)
    {
        _messageService = messageService;
    }

    [ObservableProperty]
    private string _original = string.Empty;

    [ObservableProperty]
    private string _handleText = string.Empty;

    [RelayCommand]
    private void SimplifiedToTraditional()
    {
        try
        {
            if (Original.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入要转换的内容");
                return;
            }

            HandleText = ChineseConverter.Convert(Original, ChineseConversionDirection.SimplifiedToTraditional);
        }
        catch (Exception ex)
        {
            _messageService.SendMessage($"转换失败：{ex.Message}");
        }
    }

    [RelayCommand]
    private void TraditionalToSimplified()
    {
        try
        {
            if (Original.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("请输入要转换的内容");
                return;
            }

            HandleText = ChineseConverter.Convert(Original, ChineseConversionDirection.TraditionalToSimplified);
        }
        catch (Exception ex)
        {
            _messageService.SendMessage($"转换失败：{ex.Message}");
        }
    }

    [RelayCommand]
    private void Clear()
    {
        Original = string.Empty;
        HandleText = string.Empty;
    }

    [RelayCommand]
    private async Task CopyResult()
    {
        try
        {
            if (HandleText.IsNullOrWhiteSpace())
            {
                _messageService.SendMessage("没有可复制的内容");
                return;
            }

            var topLevel = GetTopLevel();
            if (topLevel?.Clipboard is not null)
            {
                await topLevel.Clipboard.SetTextAsync(HandleText);
            }

            _messageService.SendMessage("已复制到剪贴板");
        }
        catch (Exception ex)
        {
            _messageService.SendMessage($"复制失败：{ex.Message}");
        }
    }

    private TopLevel GetTopLevel()
    {
        return TopLevel.GetTopLevel(App.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.MainWindow
            : null);
    }

    private static class ChineseConverter
    {
        private static readonly Dictionary<char, char> SimplifiedToTraditionalDict = new()
        {
            ['万'] = '萬', ['与'] = '與', ['专'] = '專', ['业'] = '業', ['东'] = '東', ['丝'] = '絲', ['丢'] = '丟', ['两'] = '兩',
            ['严'] = '嚴', ['个'] = '個', ['临'] = '臨', ['为'] = '為', ['丽'] = '麗', ['举'] = '舉', ['么'] = '麼', ['义'] = '義',
            ['乌'] = '烏', ['乐'] = '樂', ['乔'] = '喬', ['习'] = '習', ['书'] = '書', ['买'] = '買', ['乱'] = '亂', ['争'] = '爭',
            ['于'] = '於', ['亏'] = '虧', ['云'] = '雲', ['亚'] = '亞', ['产'] = '產', ['亲'] = '親', ['仅'] = '僅', ['从'] = '從',
            ['仑'] = '侖', ['仓'] = '倉', ['仪'] = '儀', ['们'] = '們', ['价'] = '價', ['众'] = '眾', ['优'] = '優', ['会'] = '會',
            ['伟'] = '偉', ['传'] = '傳', ['伤'] = '傷', ['伦'] = '倫', ['伪'] = '偽', ['体'] = '體', ['余'] = '餘', ['佣'] = '傭',
            ['侠'] = '俠', ['侣'] = '侶', ['侦'] = '偵', ['侧'] = '側', ['侨'] = '僑', ['俭'] = '儉', ['倾'] = '傾', ['偿'] = '償',
            ['储'] = '儲', ['儿'] = '兒', ['党'] = '黨', ['兰'] = '蘭', ['关'] = '關', ['兴'] = '興', ['养'] = '養', ['兽'] = '獸',
            ['冈'] = '岡', ['册'] = '冊', ['写'] = '寫', ['军'] = '軍', ['农'] = '農', ['冯'] = '馮', ['冲'] = '衝', ['决'] = '決',
            ['况'] = '況', ['冻'] = '凍', ['净'] = '淨', ['凉'] = '涼', ['减'] = '減', ['凤'] = '鳳', ['处'] = '處', ['凭'] = '憑',
            ['凯'] = '凱', ['击'] = '擊', ['刘'] = '劉', ['则'] = '則', ['刚'] = '剛', ['创'] = '創', ['删'] = '刪', ['别'] = '別',
            ['剑'] = '劍', ['剧'] = '劇', ['劝'] = '勸', ['办'] = '辦', ['务'] = '務', ['动'] = '動', ['劲'] = '勁', ['劳'] = '勞',
            ['势'] = '勢', ['勋'] = '勛', ['匀'] = '勻', ['区'] = '區', ['医'] = '醫', ['华'] = '華', ['协'] = '協', ['单'] = '單',
            ['卖'] = '賣', ['卢'] = '盧', ['卤'] = '滷', ['卧'] = '臥', ['卫'] = '衛', ['却'] = '卻', ['厂'] = '廠', ['厅'] = '廳',
            ['历'] = '歷', ['厉'] = '厲', ['压'] = '壓', ['厌'] = '厭', ['县'] = '縣', ['参'] = '參', ['双'] = '雙', ['发'] = '發',
            ['变'] = '變', ['叙'] = '敘', ['叠'] = '疊', ['台'] = '臺', ['号'] = '號', ['叹'] = '嘆', ['后'] = '後', ['向'] = '嚮',
            ['吓'] = '嚇', ['吕'] = '呂', ['吗'] = '嗎', ['吴'] = '吳', ['呕'] = '嘔', ['园'] = '園', ['围'] = '圍', ['国'] = '國',
            ['图'] = '圖', ['圆'] = '圓', ['圣'] = '聖', ['场'] = '場', ['坏'] = '壞', ['块'] = '塊', ['坚'] = '堅', ['坛'] = '壇',
            ['坝'] = '壩', ['坞'] = '塢', ['坟'] = '墳', ['坠'] = '墜', ['垄'] = '壟', ['垒'] = '壘', ['垦'] = '墾', ['垫'] = '墊',
            ['埙'] = '塤', ['埚'] = '堝', ['堕'] = '墮', ['墙'] = '牆', ['壮'] = '壯', ['声'] = '聲', ['壳'] = '殼', ['壶'] = '壺',
            ['备'] = '備', ['复'] = '復', ['够'] = '夠', ['头'] = '頭', ['夸'] = '誇', ['夹'] = '夾', ['夺'] = '奪', ['奋'] = '奮',
            ['奖'] = '獎', ['奥'] = '奧', ['妆'] = '妝', ['妇'] = '婦', ['妈'] = '媽', ['娇'] = '嬌', ['娱'] = '娛', ['娄'] = '婁',
            ['婴'] = '嬰', ['婶'] = '嬸', ['孙'] = '孫', ['学'] = '學', ['宁'] = '寧', ['宝'] = '寶', ['实'] = '實', ['宠'] = '寵',
            ['审'] = '審', ['宪'] = '憲', ['宫'] = '宮', ['宽'] = '寬', ['宾'] = '賓', ['寝'] = '寢', ['对'] = '對', ['寻'] = '尋',
            ['导'] = '導', ['寿'] = '壽', ['将'] = '將', ['尔'] = '爾', ['尘'] = '塵', ['尝'] = '嘗', ['层'] = '層', ['屉'] = '屜',
            ['届'] = '屆', ['属'] = '屬', ['屡'] = '屢', ['屿'] = '嶼', ['岁'] = '歲', ['岂'] = '豈', ['岖'] = '嶇', ['岗'] = '崗',
            ['岛'] = '島', ['岭'] = '嶺', ['峡'] = '峽', ['帏'] = '幃', ['帼'] = '幗', ['庄'] = '莊', ['庆'] = '慶', ['庐'] = '廬',
            ['库'] = '庫', ['应'] = '應', ['庙'] = '廟', ['庞'] = '龐', ['废'] = '廢', ['开'] = '開', ['异'] = '異', ['弃'] = '棄',
            ['张'] = '張', ['弥'] = '彌', ['弯'] = '彎', ['强'] = '強', ['归'] = '歸', ['当'] = '當', ['录'] = '錄', ['彦'] = '彥',
            ['彻'] = '徹', ['径'] = '徑', ['忆'] = '憶', ['态'] = '態', ['忏'] = '懺', ['忧'] = '憂', ['怀'] = '懷', ['恋'] = '戀',
            ['恶'] = '惡', ['悦'] = '悅', ['悬'] = '懸', ['悯'] = '憫', ['惊'] = '驚', ['惧'] = '懼', ['戏'] = '戲', ['战'] = '戰',
            ['户'] = '戶', ['扑'] = '撲', ['执'] = '執', ['扩'] = '擴', ['扫'] = '掃', ['扬'] = '揚', ['扰'] = '擾', ['抚'] = '撫',
            ['抛'] = '拋', ['护'] = '護', ['报'] = '報', ['担'] = '擔', ['拟'] = '擬', ['拢'] = '攏', ['拣'] = '揀', ['拥'] = '擁',
            ['拦'] = '攔', ['拨'] = '撥', ['择'] = '擇', ['挂'] = '掛', ['挚'] = '摯', ['挛'] = '攣', ['挟'] = '挾', ['挠'] = '撓',
            ['挡'] = '擋', ['挢'] = '矯', ['挽'] = '輓', ['捞'] = '撈', ['损'] = '損', ['捡'] = '撿', ['换'] = '換', ['捣'] = '搗',
            ['捻'] = '撚', ['掳'] = '擄', ['掷'] = '擲', ['掸'] = '撣', ['掺'] = '摻', ['揽'] = '攬', ['摄'] = '攝', ['摆'] = '擺',
            ['摈'] = '擯', ['摊'] = '攤', ['撑'] = '撐', ['撵'] = '攆', ['撷'] = '擷', ['撸'] = '擼', ['攒'] = '攢', ['敌'] = '敵',
            ['数'] = '數', ['敛'] = '斂', ['斋'] = '齋', ['斗'] = '鬥', ['斩'] = '斬', ['断'] = '斷', ['无'] = '無', ['旧'] = '舊',
            ['时'] = '時', ['旷'] = '曠', ['昙'] = '曇', ['昼'] = '晝', ['显'] = '顯', ['晋'] = '晉', ['晒'] = '曬', ['晓'] = '曉',
            ['晔'] = '曄', ['晕'] = '暈', ['晖'] = '暉', ['暂'] = '暫', ['暧'] = '曖', ['术'] = '術', ['朴'] = '樸', ['机'] = '機',
            ['杀'] = '殺', ['杂'] = '雜', ['权'] = '權', ['条'] = '條', ['来'] = '來', ['杨'] = '楊', ['杰'] = '傑', ['松'] = '鬆',
            ['极'] = '極', ['构'] = '構', ['枪'] = '槍', ['枫'] = '楓', ['柜'] = '櫃', ['柠'] = '檸', ['栅'] = '柵', ['栀'] = '梔',
            ['标'] = '標', ['栈'] = '棧', ['栉'] = '櫛', ['栊'] = '櫳', ['栋'] = '棟', ['栏'] = '欄', ['树'] = '樹', ['栖'] = '棲',
            ['样'] = '樣', ['桩'] = '樁', ['桥'] = '橋', ['桦'] = '樺', ['桧'] = '檜', ['桨'] = '槳', ['档'] = '檔', ['检'] = '檢',
            ['梦'] = '夢'
        };

        private static readonly Dictionary<char, char> TraditionalToSimplifiedDict = BuildReverseMap();

        public static string Convert(string text, ChineseConversionDirection direction)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }

            var dict = direction == ChineseConversionDirection.SimplifiedToTraditional
                ? SimplifiedToTraditionalDict
                : TraditionalToSimplifiedDict;

            var builder = new StringBuilder(text.Length);
            foreach (var character in text)
            {
                builder.Append(dict.TryGetValue(character, out var mapped) ? mapped : character);
            }

            return builder.ToString();
        }

        private static Dictionary<char, char> BuildReverseMap()
        {
            var result = new Dictionary<char, char>(SimplifiedToTraditionalDict.Count);
            foreach (var pair in SimplifiedToTraditionalDict)
            {
                result[pair.Value] = pair.Key;
            }

            return result;
        }
    }

    private enum ChineseConversionDirection
    {
        SimplifiedToTraditional,
        TraditionalToSimplified
    }
}
