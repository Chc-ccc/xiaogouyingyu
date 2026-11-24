namespace XiaoGouYingYu.Models;

/// <summary>四种学习模式。模式切换按钮固定在卡片顶部，四种模式随时可切。</summary>
public enum LearningMode
{
    /// <summary>整体读音：播放整条词条发音。</summary>
    WholeReading,
    /// <summary>拼读：单词类按字形段逐段读+高亮，短语/句子类按单词逐词读+高亮。</summary>
    Spelling,
    /// <summary>看中文背英文：只显示中文，输入英文判定。</summary>
    ChineseToEnglish,
    /// <summary>看中文+听音默写：显示中文并播放整体读音，隐藏英文拼写，默写判定。</summary>
    Dictation
}

/// <summary>词条复习三态。仅"标记掌握"能把词条置为 Mastered；答错自动进入 Review。</summary>
public enum WordStatus
{
    /// <summary>新词（含从待复习连对达标转回的词）。</summary>
    New,
    /// <summary>待复习（答错自动进入，可手动加入/移出）。</summary>
    Review,
    /// <summary>已掌握（仅手动标记，默认不进普通练习队列）。</summary>
    Mastered
}

/// <summary>词条分类：按主答案是否含空格或句末标点判定（先剥括号再判）。</summary>
public enum WordCategory
{
    /// <summary>单词类：四模式全支持，拼读按字形段。</summary>
    Word,
    /// <summary>短语/句子类：拼读按单词粒度，界面标"选做"。</summary>
    Phrase
}
