alter session set container = cdb$root;

----------------------------------------------------------------------------------------------------

alter system set shared_pool_size = 8m;

select name, value from v$parameter where name in ('memory_target', 'sga_target', 'shared_pool_size');
