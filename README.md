# HauppaugeIrBlaster
![GitHub release](https://img.shields.io/github/v/release/nelinory/HauppaugeIrBlaster)
![GitHub downloads](https://img.shields.io/github/downloads/nelinory/HauppaugeIrBlaster/total)
![GitHub issues](https://img.shields.io/github/issues/nelinory/HauppaugeIrBlaster)
![Github license](https://img.shields.io/github/license/nelinory/HauppaugeIrBlaster)

HauppaugeIrBlaster is an application that uses Hauppauge ir blaster to control a satellite receiver connected to Windows Media Center

## Intro

- Each day around 5:00am, the Dish satellite receiver calls home to get its update.  It then restarts itself, requiring the user to press “Select” to go to a channel.  MCE doesn’t know it needs to press select, so this causes it to miss channels and recordings.

## Features

- Wakes up the satellite receiver from standby mode (tested with Dish Network VIP211K)
- Tunes to a specified channel
- Puts MCE back to sleep if no active recordings are in progress

## Credits

- Team MediaPortal: https://www.team-mediaportal.com
- SageTv: https://github.com/google/sagetv/blob/master/third_party/Hauppauge/hcwIRblast.h