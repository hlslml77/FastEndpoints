-- 旅行系统表结构

-- 旅行关卡留言表
CREATE TABLE IF NOT EXISTS `travel_stage_message` (
  `id` BIGINT NOT NULL AUTO_INCREMENT COMMENT '自增主键',
  `user_id` BIGINT NOT NULL COMMENT '用户ID',
  `stage_id` INT NOT NULL COMMENT '关卡ID',
  `node_id` INT NOT NULL COMMENT '节点ID',
  `message_content` VARCHAR(500) NOT NULL COMMENT '留言内容',
  `created_at` DATETIME NOT NULL COMMENT '创建时间',
  PRIMARY KEY (`id`),
  KEY `idx_stage_id` (`stage_id`),
  KEY `idx_user_id` (`user_id`),
  KEY `idx_stage_node` (`stage_id`, `node_id`),
  KEY `idx_stage_node_created` (`stage_id`, `node_id`, `created_at`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci COMMENT='旅行关卡留言表';



-- 兼容已存在的旧表：补充缺失的列与索引（忽略已存在错误）
ALTER TABLE `travel_stage_message` ADD COLUMN `node_id` INT NOT NULL DEFAULT 0 AFTER `stage_id`;
ALTER TABLE `travel_stage_message` ADD INDEX `idx_stage_node` (`stage_id`, `node_id`);
ALTER TABLE `travel_stage_message` ADD INDEX `idx_stage_node_created` (`stage_id`, `node_id`, `created_at`);
