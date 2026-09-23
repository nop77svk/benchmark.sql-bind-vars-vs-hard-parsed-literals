alter session set container = freepdb1;

----------------------------------------------------------------------------------------------------

create user "BENCHMARK"
identified by "BENCHMARK"
default tablespace users
quota unlimited on users
temporary tablespace temp
;

----------------------------------------------------------------------------------------------------

grant create session, create table to benchmark;

drop table if exists benchmark.t_test_data purge;

create table benchmark.t_test_data
(
    id                  integer not null,
    constraint PK_test_data primary key (id) using index reverse
)
organization index
;
