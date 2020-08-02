#!/bin/bash

#if [ "x$1" = "x" ]; then
#  echo "version please"
#  exit 1
#fi

oldVersion="2.2.0-dev-dev"
newVersion="2.3.0-dev-dev"
#discordNetDirectory="~/dev/Discord.Net"
discordNetDirectory="/c/_stuff/dev/Discord.Net"

git rm Core/packages/*
mkdir Core/packages || true
#rm -rf ~/.nuget/packages/discord.net*
find "$discordNetDirectory" -name *.nupkg -exec cp {} Core/packages/ \;
rm Core/packages/0*

sed -i "s/$oldVersion/$newVersion/g" Core/Valkyrja.core.csproj
sed -i "s/$oldVersion/$newVersion/g" Bot/Valkyrja.discord.csproj
sed -i "s/$oldVersion/$newVersion/g" Modules/Valkyrja.modules.csproj
sed -i "s/$oldVersion/$newVersion/g" Secure/Valkyrja.secure.csproj
sed -i "s/$oldVersion/$newVersion/g" ServerSpecific/Valkyrja.specific.csproj

pushd Bot
dotnet restore
popd

pushd Modules
dotnet restore
popd

pushd Core
rm packages/Newtonsoft*
rm packages/idn*
rm packages/0*
git add packages/*
git add Valkyrja.core.csproj
git commit -m "D.NET Update"
git push
dotnet restore
popd

pushd Secure
git add Valkyrja.secure.csproj
git commit -m "D.NET Update"
git push
dotnet restore
popd

pushd ServerSpecific
git add Valkyrja.specific.csproj
git commit -m "D.NET Update"
git push
dotnet restore
popd

git add Bot/Valkyrja.discord.csproj
git add Modules/Valkyrja.modules.csproj
git commit -m "D.NET Update"
git push

echo "All done."

