# Apple 風格的材質與字體

只在刻意採用此風格時使用。

- 透明 layer 表達浮在內容上方的關係。背景複雜時增加不透明度或分隔，不連疊多層 blur。
- 確認文字在不同背景仍可讀。reduced transparency 或高對比偏好可改實色。
- 字體沿用平台或專案；支援 optical sizing 的字體可用 font-optical-sizing。中文不直接套拉丁字母的負 tracking。
- 標題、正文依字形調整 line-height，rem/em 保留使用者放大能力。
- 使用陰影與 scrim 表達前後關係，不把所有 toolbar 都改成玻璃。
- blur 動畫可能昂貴，需量測；reduced motion 可直接切換。

這些是視覺選擇，不是所有品牌必須遵守的 invariant。
