#!/bin/bash
# BCryptハッシュ値を生成してSQLファイルを更新するスクリプト

# 一時的なC#プログラムを作成
cat > /tmp/generate_hash.cs << 'EOF'
using System;
using BCrypt.Net;

class Program
{
    static void Main()
    {
        var password = "password";
        var hash = BCrypt.Net.BCrypt.HashPassword(password);
        Console.WriteLine(hash);
    }
}
EOF

# ハッシュ値を生成（dotnetスクリプトを使用）
# 注意: dotnet-scriptがインストールされている必要があります
# または、実際のアプリケーション実行時に生成されたハッシュ値を使用してください

echo "BCryptハッシュ値を生成するには、以下のいずれかの方法を使用してください:"
echo ""
echo "方法1: C#コードで生成:"
echo "  var hash = BCrypt.Net.BCrypt.HashPassword(\"password\");"
echo "  Console.WriteLine(hash);"
echo ""
echo "方法2: 実際のアプリケーション実行時に生成されたハッシュ値を使用"
echo ""
echo "生成されたハッシュ値で、scripts/seed_data.sql内の"
echo "'\$2a\$11\$PLACEHOLDER_FOR_BCRYPT_HASH'を置き換えてください。"



