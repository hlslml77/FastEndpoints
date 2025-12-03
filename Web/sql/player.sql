-- 角色系统表结构（已从 player_role_growth 迁移为 player_role）

-- 1) 新表：玩家角色数据表（仅四个主属性）
CREATE TABLE IF NOT EXISTS `player_role` (
  `user_id` BIGINT NOT NULL COMMENT '用户ID',
  `current_level` INT NOT NULL DEFAULT 1 COMMENT '当前等级',
  `current_experience` INT NOT NULL DEFAULT 0 COMMENT '当前经验值',
  `attr_upper_limb` INT NOT NULL DEFAULT 10 COMMENT '上肢属性值',
  `attr_lower_limb` INT NOT NULL DEFAULT 10 COMMENT '下肢属性值',
  `attr_core` INT NOT NULL DEFAULT 10 COMMENT '核心属性值',
  `attr_heart_lungs` INT NOT NULL DEFAULT 10 COMMENT '心肺属性值',
  `today_attribute_points` INT NOT NULL DEFAULT 0 COMMENT '今日获得的属性点',
  `stored_energy_meters` DECIMAL(10,2) NOT NULL DEFAULT 0 COMMENT '存储的能量（米），最大10000',
  `last_update_time` DATETIME NOT NULL COMMENT '最后更新时间',
  PRIMARY KEY (`user_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='玩家角色数据表';

-- 兼容已有库：增加列（如果不存在）
-- 注意：MySQL 不支持 ALTER TABLE ADD COLUMN IF NOT EXISTS，需要先检查列是否存在
-- 这里移除 IF NOT EXISTS 子句，由应用程序处理异常
ALTER TABLE `player_role`
  ADD COLUMN `stored_energy_meters` DECIMAL(10,2) NOT NULL DEFAULT 0 COMMENT '存储的能量（米），最大10000';


-- 兼容已有库：增加当前所在点位列（如果不存在）
ALTER TABLE `player_role`
  ADD COLUMN `current_location_id` INT NULL COMMENT '当前所在点位ID';
