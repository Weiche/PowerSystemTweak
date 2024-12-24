# Power System Tweak
## Version 0.1.0 Features
- 调整发电优先级,优先使用清洁能源 Optimize power production priority, prior clean energy than other power sources
- 可设置能量枢纽的放电优先级 Configurable priority of energy exchanger discharge
- 优化电力系统的多线程负载平衡，应该抵消了多余的性能消耗 Optimize power system multi-threading load balancing, which should eliminate additional performance consumption caused by function above

## Language Support
中文 & English

## 用法 Usage
1. 默认打开，可通过Ctrl + Y来切换开关，Use Ctrl + Y to toggle the switch on/off
2. 按Ctrl + U可以查看当前的电力设施处理所用时间排行 Press Ctrl + U to view the processing time ranking of power facilities

## 设置 Configuration
### PreferCleanEnergyGenerator
- 是否最优先使用清洁能源 Whether to prioritize clean energy generators first

### ExcDischargeEqualFuelGenerator
- 能量枢纽的放电优先级 Energy exchanger discharge priority
    - 0： 默认，优先级等于燃料发电机 Default, priority equals fuel generator
    - 1： 高于燃料发电机 Priority as in the original game, higher than fuel generator
    - 2： 低于燃料发电机 Priority lower than fuel generator

## 注意事项 Note
- 该Mod会完全替换电力系统处理函数(PowerSystem.GameTick)，可能会与其他mod产生冲突 This mod replaces the power system main processing function (PowerSystem.GameTick), which may conflict with other mods
- 