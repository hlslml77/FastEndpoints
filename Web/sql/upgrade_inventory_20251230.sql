-- 2025-12-30 装备随机/特殊属性改动
-- 向 player_equipment_item 表新增列（已存在则跳过）

ALTER TABLE `player_equipment_item`
    ADD COLUMN `efficiency` DOUBLE NULL COMMENT '资源效率(%)' AFTER `heart_lungs`,
    ADD COLUMN `energy` DOUBLE NULL COMMENT '能量上限(km)' AFTER `efficiency`,
    ADD COLUMN `speed` DOUBLE NULL COMMENT '平均速度(kph)' AFTER `energy`,
    ADD COLUMN `special_entry_id` INT NULL COMMENT '特殊词条ID' AFTER `speed`;

-- 如果字段已存在，可根据实际情况删除对应语句或使用 CHANGE/MODIFY 调整类型。
