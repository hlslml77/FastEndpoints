-- 角色系统表结构（已从 player_role_growth 迁移为 player_role）

-- 1) 新表：玩家角色数据表（包含四个主属性与按配表换算的九项副属性）
CREATE TABLE IF NOT EXISTS `player_role` (
  `user_id` BIGINT NOT NULL COMMENT '用户ID',
  `current_level` INT NOT NULL DEFAULT 1 COMMENT '当前等级',
  `current_experience` INT NOT NULL DEFAULT 0 COMMENT '当前经验值',
  `attr_upper_limb` INT NOT NULL DEFAULT 10 COMMENT '上肢属性值',
  `attr_lower_limb` INT NOT NULL DEFAULT 10 COMMENT '下肢属性值',
  `attr_core` INT NOT NULL DEFAULT 10 COMMENT '核心属性值',
  `attr_heart_lungs` INT NOT NULL DEFAULT 10 COMMENT '心肺属性值',
  `today_attribute_points` INT NOT NULL DEFAULT 0 COMMENT '今日获得的属性点',
  -- 副属性（由四个主属性按 Role_Attribute.json 中每点加成换算）
  `sec_attack` DECIMAL(18,4) NOT NULL DEFAULT 0 COMMENT '副属性-攻击',
  `sec_hp` DECIMAL(18,4) NOT NULL DEFAULT 0 COMMENT '副属性-生命',
  `sec_defense` DECIMAL(18,4) NOT NULL DEFAULT 0 COMMENT '副属性-防御',
  `sec_attack_speed` DECIMAL(18,4) NOT NULL DEFAULT 0 COMMENT '副属性-攻速',
  `sec_critical` DECIMAL(18,4) NOT NULL DEFAULT 0 COMMENT '副属性-暴击',
  `sec_critical_damage` DECIMAL(18,4) NOT NULL DEFAULT 0 COMMENT '副属性-爆伤',
  `sec_speed` DECIMAL(18,4) NOT NULL DEFAULT 0 COMMENT '副属性-速度',
  `sec_efficiency` DECIMAL(18,4) NOT NULL DEFAULT 0 COMMENT '副属性-效率',
  `sec_energy` DECIMAL(18,4) NOT NULL DEFAULT 0 COMMENT '副属性-能量',
  `last_update_time` DATETIME NOT NULL COMMENT '最后更新时间',
  PRIMARY KEY (`user_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='玩家角色数据表';

-- 2) 迁移：如果旧表存在，将旧数据插入新表（避免重复）
INSERT INTO `player_role` (
  `user_id`, `current_level`, `current_experience`, `attr_upper_limb`, `attr_lower_limb`, `attr_core`, `attr_heart_lungs`, `today_attribute_points`, `last_update_time`
)
SELECT g.`user_id`, g.`current_level`, g.`current_experience`, g.`attr_upper_limb`, g.`attr_lower_limb`, g.`attr_core`, g.`attr_heart_lungs`, g.`today_attribute_points`, g.`last_update_time`
FROM `player_role_growth` g
LEFT JOIN `player_role` r ON r.`user_id` = g.`user_id`
WHERE r.`user_id` IS NULL;

-- 2.1) 为已有表补充副属性列（兼容增量执行）
ALTER TABLE `player_role`
  ADD COLUMN IF NOT EXISTS `sec_attack` DECIMAL(18,4) NOT NULL DEFAULT 0 COMMENT '副属性-攻击',
  ADD COLUMN IF NOT EXISTS `sec_hp` DECIMAL(18,4) NOT NULL DEFAULT 0 COMMENT '副属性-生命',
  ADD COLUMN IF NOT EXISTS `sec_defense` DECIMAL(18,4) NOT NULL DEFAULT 0 COMMENT '副属性-防御',
  ADD COLUMN IF NOT EXISTS `sec_attack_speed` DECIMAL(18,4) NOT NULL DEFAULT 0 COMMENT '副属性-攻速',
  ADD COLUMN IF NOT EXISTS `sec_critical` DECIMAL(18,4) NOT NULL DEFAULT 0 COMMENT '副属性-暴击',
  ADD COLUMN IF NOT EXISTS `sec_critical_damage` DECIMAL(18,4) NOT NULL DEFAULT 0 COMMENT '副属性-爆伤',
  ADD COLUMN IF NOT EXISTS `sec_speed` DECIMAL(18,4) NOT NULL DEFAULT 0 COMMENT '副属性-速度',
  ADD COLUMN IF NOT EXISTS `sec_efficiency` DECIMAL(18,4) NOT NULL DEFAULT 0 COMMENT '副属性-效率',
  ADD COLUMN IF NOT EXISTS `sec_energy` DECIMAL(18,4) NOT NULL DEFAULT 0 COMMENT '副属性-能量';

-- 3) 可选：保留旧表不删除（如果要删除，请取消注释下面语句）
-- DROP TABLE IF EXISTS `player_role_growth`;
