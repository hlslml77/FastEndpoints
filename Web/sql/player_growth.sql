-- 角色成长系统表结构
-- 创建玩家角色成长数据表
CREATE TABLE IF NOT EXISTS `player_role_growth` (
  `user_id` BIGINT NOT NULL COMMENT '用户ID',
  `current_level` INT NOT NULL DEFAULT 1 COMMENT '当前等级',
  `current_experience` INT NOT NULL DEFAULT 0 COMMENT '当前经验值',
  `attr_upper_limb` INT NOT NULL DEFAULT 10 COMMENT '上肢属性值',
  `attr_lower_limb` INT NOT NULL DEFAULT 10 COMMENT '下肢属性值',
  `attr_core` INT NOT NULL DEFAULT 10 COMMENT '核心属性值',
  `attr_heart_lungs` INT NOT NULL DEFAULT 10 COMMENT '心肺属性值',
  `today_attribute_points` INT NOT NULL DEFAULT 0 COMMENT '今日获得的属性点',
  `last_update_time` DATETIME NOT NULL COMMENT '最后更新时间',
  PRIMARY KEY (`user_id`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='玩家角色成长数据表';
