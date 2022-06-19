# MSBuild_Practice

## MSBuildとは

.NETを使うプログラム開発のためのビルドプラットフォーム。  
プログラムのコンパイルだけでなく、フォルダを作成したり実行ファイルの実行といった開発に必要な作業をcsprojファイルに記述しておくだけでまとめて自動で行ってくれる。

## 練習環境

- Windows10

- .NET Framework 4.8

- PowerShell

## 何が嬉しいのか

開発用のディレクトリを作って、そこに実行ファイルをコンパイルするようなことは、何もMSBuildを使わなくてもできる。

```PowerShell
$base = (Get-Location).Path

$filename = "sample.cs"

$dirname = "Test"

$result = "main.exe"

New-Item $dirname -ItemType Directory

csc.exe -target:exe -out:"$base\$dirname\$result" "$base\$filename"

& .\$dirname\$result
```

カレントディレクトリ内に `Test` ディレクトリを作成し、 `カレントディレクトリ\Test\` 内に `main.exe` をコンパイルしてそれを実行するバッチファイル。

例えばディレクトリ名を `Test2` に変えたい場合...

```PowerShell
- $dirname = "Test"
+ $dirname = "Test2"

=> "est2" とか書き間違える可能性がある
```

スクリプトファイルの場合(.batでも.ps1でもいい)実体はただのtxtファイルなので毎回直接書き換える必要がある。この過程でヒューマンエラーが発生する可能性がある。

MSBuildの場合...

```XML
<PropertyGroup>
    <OutputPath>Test<OutputPath>
</PropertyGroup>
```

```PowerShell
msbuild.exe program.csproj -p:OutputPath=Test2
```

とする事で出力フォルダを表す `OutputPath` プロパティの値を一時的に `Test2` に書き換えることができる。変更はその場だけでcsprojファイル上は `Test` のまま。バッチファイルを書き換えて実行するよりも、コマンドライン上で書き換える方が直接視認しているのでミスに気づきやすい。  

仮に直接csprojファイルを書き換える場合でも、csprojファイルはXMLを基にしているため `<プロパティ>値</プロパティ>` の書き方になっており値の変更が分かりやすい。

## タスクの切り替え、連続実行

例えばリビルドのためにTestディレクトリごと削除したいとする。そのままだと...

```PowerShell
Remove-Item .\Test -Recurse -Force
```

を毎回コマンドラインに入力するか、別のバッチファイルとして保存して呼び出す必要がある。この場合も `Test => Test2` にディレクトリ名が変わってしまった場合は毎回書き換える必要がある。非常に面倒で、誤って別のフォルダを消してしまう可能性もある。

これがMSBuildだと

```XML
<Target Name="Clean">
    <RemoveDir Directories="$(OutputPath)"/>
</Target>
```

```PowerShell
msbuild.exe program.csproj -t:Clean  ※(-p:OutputPath=Test2)
```

MSBuildでは実行したい作業(ターゲット)を複数用意しておくことができる。 `Target` ノードに実行したいタスクを子要素として渡しておき、各種プロパティを設定しておく。呼び出す時は `t:Name` としてTargetノードをコマンドラインから呼び出すことでプログラムのビルド以外にも開発作業において必要な作業を実行させることができる。

`RemoveDir` タスクは `Directories` プロパティに設定したディレクトリとその配下のファイルを削除するタスク。 `OutputPath` がTestのままなら、そのまま呼び出すだけでディレクトリを削除できる。OutputPathを `Test2` に変えていた時は-pオプションを使って動的に「Test2」を与えてやれば、csprojファイルを変更することなくディレクトリを削除できる。

タスクは一度に複数を同時に実行させることができる。例えばバッチファイルだと...

```PowerShell
Remove-Item Test -Recurse -Force

csc.exe -target:exe -out:"$base\$dirname\$result" "$base\$filename"
```

と記述しないといけない。もし処理を逆の順番で行いたい場合は、 `スクリプトをコピーして逆の順番で貼り付ける` ような事をしないといけない。その過程で間違えてスクリプトを消してしまう可能性がある。  

そもそも、何度も使うような大事なバッチファイルを不必要に改変することはミスに繋がるので良くない。

MSBuildの場合...

```XML
<Target Name="Build">
    ...
</Target>

<Target Name="Clean">
    <RemoveDir Directories="$(RootFolder)" />
</Target>

<Target Name="Rebuild" DependsOnTargets="Clean;Build" />
```

```PowerShell
msbuild.exe program.csproj -t:Rebuild
```

`Rebuild` ターゲットには`DependsOnTargets="Clean;Build"` 属性を設定している。この状態でRebuildターゲットを実行させると `Clean` `Build` ターゲットの順番でタスクを実行してくれる。  
行いたい作業をターゲットとして作っておけば、後はそれを組み合わせることで自分の好きな作業を実行させることができる。一連の流れを毎回記述する必要がなく、作業内容を使い回すことができる。  

csprojファイルの中では完全にCleanターゲットとBuildターゲットを区別して記述することができるので可読性やメンテナンス性も向上する。Cleanターゲットをメンテナンスしたければ、Cleanターゲットだけを書き換えれば良い。

## 今回のプログラムの一部説明

```XML
<Project DefaultTargets="Build" ...>
```

毎回実行したいターゲットを設定するのは面倒。  
 `DefaultTargets=Build` と設定しておく事で `msbuild.exe program.csproj` と何もターゲットオプションを与えずに実行する時はBuildターゲットを実行するよう命令できる。

```XML
<PropertyGroup>
    <Configuration Condition=" '$(Configuration)' == '' ">Debug</Configuration>
    <AssemblyName>Main</AssemblyName>
    <RootFolder>Bin</RootFolder>
    <Platform>x64</Platform>
</PropertyGroup>
```

各種ターゲットを実行する時に渡したいプロパティは、 `PropertyGroup` の子要素として設定しておく。  
ここでは `-p:Configuration=Release` のようにコマンドラインから値を渡していない時には自動で`<Configuration>Debug</Configuration>` となるようにしている。  
`Condition` 属性を使う事でプロパティも動的に変更することができる。

```XML
<PropertyGroup Condition=" '$(Configuration)' == 'Debug' ">
    <OutputPath>$(RootFolder)\Debug\</OutputPath>
</PropertyGroup>
```

「開発時と本番のディレクトリを区別したい」という事がある。今回は出力先ディレクトリをConfigurationの属性の値で分けている。  
上記の例だと `Configuration == Debug` の時は `$(RootFolder)\Debug\` にしている。すなわちデバッグモードの時には `Bin\Debug\` 下に実行ファイルがコンパイルされる。  
`Configuration == Release` の時にはリリースモードで `Bin\Release\` 下に実行ファイルがコンパイルされる。  
プロパティの値でコンパイル先を動的に変更できる。

```XML
<CSC Sources="@(Compile)" OutputAssembly="$(OutputPath)$(AssemblyName).exe" TargetType="exe" DefineConstants="DEBUG" />
<CSC Condition=" '$(Configuration)' == 'Release' " Sources="@(Compile)" OutputAssembly="$(OutputPath)$(AssemblyName).exe" TargetType="exe" Optimize="true" />
```

Buildターゲット内のCSCタスク処理。ここでプログラムをどうコンパイルするか記述する。ここでも `Configuration` の値で処理を分けている。`Debug` の場合は最適化処理はせずにDEBUGシンボルを付けてコンパイルしている。

```CSharp
#if DEBUG
    Console.WriteLine("DEBUG");
#else
    Console.WriteLine("Hello World!");
#endif
```

としているので、デバッグ時には「DEBUG」と表示される。
`Release` の時には最適化処理をしてコンパイルし、DEBUGシンボルを渡さずにコンパイルしている。よって「Hello World!」と表示される。  
開発時の処理、本番時の処理を最低限の変更で行うことができる。

## 終わり

MSBuildが「コンパイルツール」ではないのは、プログラムのコンパイルだけでなく様々な「開発作業に必要な処理」を自動で行わせることができるから。そのため「ビルドプラットフォーム」になる。  
疎結合で組み合わせやすいよう、管理しやすいようにターゲットを分けて記述していったり、データのミスが起きない単なる文字列ではなくより分かりやすいプロパティとして値を管理したり、直接設定ファイルを触らせないようにコマンドラインからプロパティ値を上書きできたりするのは便利。

「できるだけ設定ファイルを触らせない、まずミスを起こさせない環境を作る」事はMSBuildだけでなくプログラムを作る上で非常に重要である。

## 参考

[MSBuild プロジェクト ファイルのゼロからの作成](https://docs.microsoft.com/ja-jp/visualstudio/msbuild/walkthrough-creating-an-msbuild-project-file-from-scratch?view=vs-2022)