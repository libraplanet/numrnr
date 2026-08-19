# 環境

# コマンド

## プロジェクト作成

```
dotnet new winforms -f net48
```

### .NET Framework 4.8

```
<PropertyGroup>
  <TargetFramework>net48</TargetFramework>
</PropertyGroup>
```

### Winform対応

```
<PropertyGroup>
  <UseWindowsForms>true</UseWindowsForms>
</PropertyGroup>
```

### x86化

P/Invokeを使うため、x86を指定する。

```
<PropertyGroup>
  <PlatformTarget>x86</PlatformTarget>
</PropertyGroup>
```

### C# 8.0

C#の言語バージョンを変更する。

```
<PropertyGroup>
  <LangVersion>8.0</LangVersion>
</PropertyGroup>
```



### 自動Importを無効化

import分くらい自分で書け。

```
<PropertyGroup>
  <ImplicitUsings>enable</ImplicitUsings>
</PropertyGroup>
```

### ソースはsrcフォルダーから読む

```
<PropertyGroup>
  <!-- 自動でソース ファイル (*.cs) を探すのを無効化. -->
  <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
</PropertyGroup>

<!-- ソース フォルダーの追加. -->
<ItemGroup>
  <Compile Include="src\**\*.cs" />
</ItemGroup>
```



## 開発

### build (debug build)

```
dotnet build
dotnet build --configuration Debug
dotnet build -c Debug
```

### release build

```
dotnet build --configuration Release
dotnet build -c Release
```

### run

```
dotnet run
dotnet run -c Release
```


### publish

```
dotnet publish -c Release -o ./publish
```

### clean

```
dotnet clean
```

