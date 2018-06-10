#!/bin/bash

if [ "x$1" = "x" ]; then
  echo "version please"
  exit 1
fi

git rm packages/*
mkdir packages
mv ~/Downloads/Discord.Net.* packages/
rm -rf ~/.nuget/packages/discord.net*
git add packages/

sed -i "s/2.0.0-beta2-[0-9]*/2.0.0-beta2-$1/g" Core/Botwinder.core.csproj
sed -i "s/2.0.0-beta2-[0-9]*/2.0.0-beta2-$1/g" Bot/Botwinder.discord.csproj
sed -i "s/2.0.0-beta2-[0-9]*/2.0.0-beta2-$1/g" Modules/Botwinder.modules.csproj
sed -i "s/2.0.0-beta2-[0-9]*/2.0.0-beta2-$1/g" Secure/Botwinder.secure.csproj

git add Bot/Botwinder.discord.csproj
git add Modules/Botwinder.modules.csproj
git commit -m "D.NET Update"
git push
cd Bot
dotnet restore
cd ../Modules
dotnet restore

cd ../Core
git add Botwinder.core.csproj
git commit -m "D.NET Update"
git push
dotnet restore

cd ../Secure
git add Botwinder.secure.csproj
git commit -m "D.NET Update"
git push
dotnet restore

