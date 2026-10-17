-- Insert-only role for application logging. Run once in the Supabase SQL editor,
-- after the API has started once (its migration creates the photoworld schema and tables).
-- Replace the password, then use this role in ConnectionStrings:Logging.

create role photoworld_logger with login password 'CHANGE-ME-to-a-long-random-password';

grant usage on schema photoworld to photoworld_logger;
grant insert on photoworld.app_logs to photoworld_logger;
-- No select/update/delete: a leaked UI config can write logs but can't read or alter data.
