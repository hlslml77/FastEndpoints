-- 库存系统表结构

-- 1) 玩家道具表（简单的数量记录）
CREATE TABLE IF NOT EXISTS `player_item` (
  `user_id` BIGINT NOT NULL COMMENT '用户ID',
  `item_id` INT NOT NULL COMMENT '道具ID',
  `amount` BIGINT NOT NULL DEFAULT 0 COMMENT '道具数量',
  `updated_at` DATETIME NOT NULL COMMENT '更新时间',
  PRIMARY KEY (`user_id`, `item_id`),
  INDEX `idx_user_id` (`user_id`),
  INDEX `idx_updated_at` (`updated_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='玩家道具表';

-- 2) 玩家装备表
CREATE TABLE IF NOT EXISTS `player_equipment_item` (
  `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '装备实例ID',
  `user_id` BIGINT NOT NULL COMMENT '用户ID',
  `equip_id` INT NOT NULL COMMENT '装备配置ID',
  `quality` INT NOT NULL COMMENT '品质等级',
  `part` INT NOT NULL COMMENT '装备部位',
  `attack` INT COMMENT '攻击力',
  `hp` INT COMMENT '生命值',
  `defense` INT COMMENT '防御力',
  `critical` INT COMMENT '暴击率',
  `attack_speed` INT COMMENT '攻击速度',
  `critical_damage` INT COMMENT '暴击伤害',
  `upper_limb` INT COMMENT '上肢属性加成',
  `lower_limb` INT COMMENT '下肢属性加成',
  `core` INT COMMENT '核心属性加成',
  `heart_lungs` INT COMMENT '心肺属性加成',
  `is_equipped` BOOLEAN NOT NULL DEFAULT FALSE COMMENT '是否已装备',
  `created_at` DATETIME NOT NULL COMMENT '创建时间',
  `updated_at` DATETIME NOT NULL COMMENT '更新时间',
  PRIMARY KEY (`id`),
  INDEX `idx_user_id` (`user_id`),
  INDEX `idx_equip_id` (`equip_id`),
  INDEX `idx_is_equipped` (`is_equipped`),
  INDEX `idx_created_at` (`created_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='玩家装备表';

