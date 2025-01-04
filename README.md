# Power System Tweak
## Version 1.0.2 Features
- 调整发电优先级,优先使用清洁能源 Optimize power production priority, prior clean energy than other power sources
- 可设置能量枢纽的放电优先级 Configurable priority of energy exchanger discharge
- 优化电力系统的多线程负载平衡，应该抵消了多余的性能消耗 Optimize power system multi-threading load balancing, which should eliminate additional performance consumption caused by function above

## Language Support
中文 & English

## 用法 Usage
1. 默认打开，可通过Ctrl + Y来切换开关，Use Ctrl + Y to toggle the switch on/off
2. 按Ctrl + Shift + Y 来循环切换能量枢纽的放电优先级 Press Ctrl + Shift + Y to cycle through the priority of energy exchanger discharge
3. 按Ctrl + U可以查看当前的电力设施处理所用时间排行 Press Ctrl + U to view the processing time ranking of power facilities

## 设置 Configuration
### PreferCleanEnergyGenerator
- 是否最优先使用清洁能源 Whether to prioritize clean energy generators first

### ExcDischargeEqualFuelGenerator
- 能量枢纽的放电优先级 Energy exchanger discharge priority
    - 0： 默认，优先级等于燃料发电机 Default, priority equals fuel generator
    - 1： 高于燃料发电机 Priority as in the original game, higher than fuel generator
    - 2： 低于燃料发电机 Priority lower than fuel generator

## 注意事项 Note
- 该Mod会完全替换电力系统处理PowerSystem.GameTick，如果发现有其他MOD也对这个函数加了Patch，本MOD将自动放弃patch并失效。 Please note that this mod will completely replace the power system processing function PowerSystem.GameTick, and if other mods also patch this function, this feature will automatically give up to patch the method and become invalid.

## 版本 Changelog
### 1.0.2
- 更新GameTick的Patch到对应0.10.31
- 增加在游戏中切换能量枢纽放电优先级的功能 Add function to cycle through the priority of energy exchanger discharge in-game

### 1.0.0
- 初始版本 Initial release