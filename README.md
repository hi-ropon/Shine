# Shine

Visual Studio用のAIアシスタント拡張機能です。OpenAIまたはAzure OpenAIのAPIを利用してコードに関する質問や提案を受けることができます。

## 機能

- OpenAI / Azure OpenAI APIを利用したチャット機能
- ファイルコンテンツの参照機能 (@filename)
- 開いているファイル全体の参照機能
- Visual Studioのテーマに合わせた表示

## 必要要件

- Visual Studio 2022 
- .NET Framework 4.7.2以上

## 使用しているNuGetパッケージ

- Microsoft.VisualStudio.SDK (17.7.37357)  
  - License: [Microsoft Software License](https://www.nuget.org/packages/Microsoft.VisualStudio.SDK/17.7.37357/license)

- Microsoft.Web.WebView2 (1.0.2210.55)  
  - License: [BSD-3-Clause](https://www.nuget.org/packages/Microsoft.Web.WebView2/1.0.2210.55/license)

- Azure.AI.OpenAI (1.0.0-beta.8)  
  - License: [MIT](https://www.nuget.org/packages/Azure.AI.OpenAI/1.0.0-beta.8/license)

- Markdig (最新バージョン)  
  - License: [MIT License](https://opensource.org/licenses/MIT)

- Markdig.Syntax (最新バージョン)  
  - License: [MIT License](https://opensource.org/licenses/MIT)

- OpenAI (最新バージョン)  
  - License: [MIT License](https://opensource.org/licenses/MIT)

## セットアップ

1. Visual Studioの拡張機能マネージャーからインストール
2. ツール > オプション > Code Assistant Tool から設定を行う
   - OpenAIまたはAzure OpenAIの認証情報を設定
   - 使用するモデルを選択

## OpenAIについて

本プロジェクトでは、OpenAIおよびAzure OpenAIのAPIを活用して、コード生成や補助機能を提供しています。  
これにより、ユーザーは高度な自然言語処理の技術を利用して、より効率的に開発を進めることができます。

## Markdigについて

Markdownのレンダリングには、高速で柔軟なMarkdigライブラリを使用しています。  
これにより、Visual Studio内での表示やコンテンツの整形が正確に行われます。

## ライセンス

このプロジェクトはMITライセンスの下で公開されています。

各コンポーネントのライセンス情報:

### Microsoft.VisualStudio.SDK
- Copyright (c) Microsoft Corporation
- [Microsoft Software License Terms](https://www.nuget.org/packages/Microsoft.VisualStudio.SDK/17.7.37357/license)

### Microsoft.Web.WebView2
- Copyright (c) Microsoft Corporation
- [BSD-3-Clause License](https://www.nuget.org/packages/Microsoft.Web.WebView2/1.0.2210.55/license)

### Azure.AI.OpenAI
- Copyright (c) Microsoft Corporation
- [MIT License](https://www.nuget.org/packages/Azure.AI.OpenAI/1.0.0-beta.8/license)

### Markdig
- [MIT License](https://opensource.org/licenses/MIT)

### Markdig.Syntax
- [MIT License](https://opensource.org/licenses/MIT)

### OpenAI
- [MIT License](https://opensource.org/licenses/MIT)
