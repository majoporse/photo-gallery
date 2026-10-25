FROM alpine:3.20

# imagemagick-jpeg is a separate package on Alpine; without it `magick` has no
# JPEG encoder at all.
RUN apk add --no-cache curl imagemagick imagemagick-jpeg jq

COPY seed.sh /seed.sh
RUN chmod +x /seed.sh

CMD ["/seed.sh"]
