# SpikeFinder

SpikeFinder is a program designed to see the biometry data of the Lenstar in more detail than EyeSuite makes available. SpikeFinder reads data directly from the Lenstar's MySQL database. It does not edit or update Lenstar data, and saves its own data outside of the Lenstar's database.

This program is **not** designed by Haag-Streit, and is intended **for research use only**. It is provided as-is. I claim no responsibility if you mess up your Lenstar while trying to make this software work.

## Getting Started

Download the installer on [the releases page](https://github.com/timothylcooke/SpikeFinder/releases). You can run it on the Lenstar itself, or on any other computer that can connect to the Lenstar's database. You may need help from your IT department configuring MySQL to allow you to connect to the database.

## Support

Feel free to create an issue [on the issues page](https://github.com/timothylcooke/SpikeFinder/issues) if you find a bug, or if you would like a new feature added.

You can also email <tcooke@greateyecare.com>, or contact me at [+1-269-313-7576](tel:+12693137576) via SMS (US/Canadian numbers only), WhatsApp, or Signal.

## Thanks!

Thanks for your interest in this project. Hopefully you find it as useful as we have.

## Permissions

Whatever user you connect to MySQL only needs access to SELECT from the following tables:
- tbl_basic_examination
- tbl_basic_patient
- tbl_bio_ascan
- tbl_bio_biometry
- tbl_bio_biometry_cursors
- tbl_bio_biometry_dimensions
- tbl_bio_biometry_setting_status
- tbl_bio_keratometry
- tbl_bio_keratometry_settings
- tbl_bio_measurement
- tbl_bio_pupilometry
- tbl_bio_whitewhite
