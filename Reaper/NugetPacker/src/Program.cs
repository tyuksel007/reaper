﻿
using NugetPacker;

Version version = new(1, 1, 242);
string nugetPath = "/home/tyfn/localNugetStore";
string commonLibProjectPath = "../../CommonLib/src/";
string signaleSentinelProjectPath = "../../SignalSentinel/src/";
string binancProjectPath = "../../Exchanges/Binance/src/";
string kucoinProjectPath = "../../Exchanges/Kucoin/src/";

Packer.Pack(commonLibProjectPath, nugetPath, version);
Packer.Pack(signaleSentinelProjectPath, nugetPath, version);
Dictionary<string, string> localNugetStore = Packer.GetLocalNugets(nugetPath);

void UpdateBinance() => Packer.GetCsProjFiles(binancProjectPath).UpdateWith(localNugetStore); 
void UpdateKucoin() => Packer.GetCsProjFiles(kucoinProjectPath).UpdateWith(localNugetStore); 
UpdateBinance();
UpdateKucoin();


