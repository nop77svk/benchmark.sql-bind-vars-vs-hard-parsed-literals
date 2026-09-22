create user benchmark
identified by benchmark
default tablespace users
quota unlimited on users
temporary tablespace temp
;

----------------------------------------------------------------------------------------------------

grant create session, create table to benchmark;

drop table if exists benchmark.t_test_data purge;

create table benchmark.t_test_data
(
    id                  integer not null primary key
)
organization index
;
