# EtherMineSafeRunner
*Watch utility for the ethminer - Ethereum GPU mining worker software.*

### Description

This is a watch utility to provide steady operation of the [ethminer](https://github.com/ethereum-mining/ethminer) - Ethereum GPU mining worker software.

This program was designed to do the following:

- To recover from crashes in the ethminer by restarting it.
- To restart ethminer if the hash-rate is outside of the provided range.
- To restart ethminer if the mining pool hasn't accepted hashes for a while.
- To reboot the mining rig after a certain number of restarts of the ethminer.
- To maintain the diagnostic event log of the operation of ethminer in a persistent text file.

For more details check [this blog post](https://dennisbabkin.com/blog/?t=patching-bugs-ethminer-watch-utility).


### Release Build

Is available [here](https://dennisbabkin.com/blog/?t=patching-bugs-ethminer-watch-utility).

### Basic Operation

Usage:
> EtherMineSafeRunner MinHash MaxHash RebootAfter "path-to\ethminer.exe" miner_commad_line

- `MinHash`      = mininum allowed hash rate range (in Mh), ex: 80
- `MaxHash`      = maximum allowed hash rate range (in Mh), or 0 if any, ex: 100
- `RebootAfter`  = number of ethminer restarts before rebooting the rig, or 0 not to reboot
-------------

#### Examples:

>EtherMineSafeRunner 80 100 32 "path-to\ethminer.exe" -P stratum://0xETH_PUB_KEY:x@us2.ethermine.org:14444


### Build Instructions

Use Visual Studio 2019, Community edition. Build as Release configuration.


--------------

Submit suggestions & bug reports [here](https://www.dennisbabkin.com/sfb/?what=bug&name=EtherMineSafeRunner&ver=Github).
