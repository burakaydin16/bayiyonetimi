using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MultiTenantSaaS.Migrations
{
    /// <inheritdoc />
    public partial class AddUserVerificationTokens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Safely add new columns to public.users using DO blocks to avoid errors if they already exist
            migrationBuilder.Sql(@"
                DO $$
                BEGIN
                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='users' AND column_name='is_email_verified') THEN
                        ALTER TABLE public.users ADD COLUMN is_email_verified boolean NOT NULL DEFAULT false;
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='users' AND column_name='email_verification_token') THEN
                        ALTER TABLE public.users ADD COLUMN email_verification_token text;
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='users' AND column_name='email_verification_token_expiry') THEN
                        ALTER TABLE public.users ADD COLUMN email_verification_token_expiry timestamp with time zone;
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='users' AND column_name='password_reset_token') THEN
                        ALTER TABLE public.users ADD COLUMN password_reset_token text;
                    END IF;

                    IF NOT EXISTS (SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='users' AND column_name='password_reset_token_expiry') THEN
                        ALTER TABLE public.users ADD COLUMN password_reset_token_expiry timestamp with time zone;
                    END IF;
                END $$;
            ");

            // Mark all existing users as already verified (they were registered before this feature)
            migrationBuilder.Sql(@"
                UPDATE public.users SET is_email_verified = true WHERE is_email_verified = false;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                ALTER TABLE public.users
                    DROP COLUMN IF EXISTS is_email_verified,
                    DROP COLUMN IF EXISTS email_verification_token,
                    DROP COLUMN IF EXISTS email_verification_token_expiry,
                    DROP COLUMN IF EXISTS password_reset_token,
                    DROP COLUMN IF EXISTS password_reset_token_expiry;
            ");
        }
    }
}
